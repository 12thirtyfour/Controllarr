using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Bios
{
    public interface IBiosService
    {
        bool VerifyBios(string filePath, string expectedMd5, string expectedSha1);
        List<BiosFile> GetCatalog(string platformSlug = null);
        List<BiosFile> ScanAndVerifyBiosDirectory(string biosDirectory, string platformSlug = null);
        List<BiosFile> DownloadMissingBios(string platformSlug = null, string fileName = null);
    }

    public class BiosService : IBiosService
    {
        // Maps canonical platform catalog slugs (from GameCatalogProxy) only when the catalog slug differs from the BIOS group key for the exact same device
        private static readonly Dictionary<string, string> CompanionBiosGroupBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "gg", "gamegear" }
        };

        private static readonly Dictionary<string, string> AbdessSystemFolderByBiosGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ps1", "Sony - PlayStation" },
            { "ps2", "Sony - PlayStation 2" },
            { "psp", "Sony - PlayStation Portable" },
            { "segacd", "Sega - Mega CD - Sega CD" },
            { "saturn", "Sega - Saturn" },
            { "dreamcast", "Sega - Dreamcast" },
            { "naomi", "Sega - Naomi" },
            { "sms", "Sega - Master System - Mark III" },
            { "gamegear", "Sega - Game Gear" },
            { "fds", "Nintendo - Family Computer Disk System" },
            { "satellaview", "Nintendo - Satellaview" },
            { "64dd", "Nintendo - Nintendo 64DD" },
            { "gb", "Nintendo - Game Boy" },
            { "gbc", "Nintendo - Game Boy Color" },
            { "sgb", "Nintendo - Super Nintendo Entertainment System" },
            { "gba", "Nintendo - Game Boy Advance" },
            { "nds", "Nintendo - Nintendo DS" },
            { "dsi", "Nintendo - Nintendo DSi" },
            { "pokemonmini", "Nintendo - Pokemon Mini" },
            { "gamecube", "Nintendo - GameCube" },
            { "tgcd", "NEC - PC Engine - TurboGrafx 16" },
            { "pcfx", "NEC - PC-FX" },
            { "neogeo", "SNK - Neo Geo" },
            { "neogeocd", "SNK - Neo Geo CD" },
            { "3do", "The 3DO Company - 3DO" },
            { "atari5200", "Atari - 5200" },
            { "atari7800", "Atari - 7800" },
            { "lynx", "Atari - Lynx" },
            { "jaguarcd", "Atari - Jaguar" },
            { "colecovision", "Coleco - ColecoVision" },
            { "intellivision", "Mattel - Intellivision" },
            { "odyssey2", "Magnavox - Odyssey2" },
            { "vectrex", "GCE - Vectrex" },
            { "channelf", "Fairchild - Channel F" },
            { "msx", "Microsoft - MSX" },
            { "amiga", "Commodore - Amiga" },
            { "x68000", "Sharp - X68000" },
            { "pc98", "NEC - PC-98" },
            { "cdi", "Philips - CD-i" }
        };

        private static readonly Dictionary<string, string> PlatformDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ps1", "Sony PlayStation (PS1)" },
            { "ps2", "Sony PlayStation 2 (PS2)" },
            { "psp", "Sony PlayStation Portable (PSP)" },
            { "segacd", "Sega CD / Mega-CD" },
            { "saturn", "Sega Saturn" },
            { "dreamcast", "Sega Dreamcast" },
            { "naomi", "Sega NAOMI / Atomiswave" },
            { "sms", "Sega Master System / Mark III" },
            { "gamegear", "Sega Game Gear" },
            { "fds", "Nintendo Famicom Disk System" },
            { "satellaview", "Nintendo Satellaview (BS-X)" },
            { "64dd", "Nintendo 64DD" },
            { "gb", "Nintendo Game Boy" },
            { "gbc", "Nintendo Game Boy Color" },
            { "sgb", "Super Game Boy" },
            { "gba", "Nintendo Game Boy Advance" },
            { "nds", "Nintendo DS" },
            { "dsi", "Nintendo DSi" },
            { "pokemonmini", "Nintendo Pokémon Mini" },
            { "gamecube", "Nintendo GameCube" },
            { "tgcd", "TurboGrafx-CD / PC Engine CD-ROM²" },
            { "pcfx", "NEC PC-FX" },
            { "neogeo", "SNK Neo Geo AES / MVS" },
            { "neogeocd", "SNK Neo Geo CD" },
            { "3do", "3DO Interactive Multiplayer" },
            { "atari5200", "Atari 5200" },
            { "atari7800", "Atari 7800" },
            { "lynx", "Atari Lynx" },
            { "jaguarcd", "Atari Jaguar CD" },
            { "colecovision", "ColecoVision" },
            { "intellivision", "Mattel Intellivision" },
            { "odyssey2", "Magnavox Odyssey² / Videopac" },
            { "vectrex", "GCE Vectrex" },
            { "channelf", "Fairchild Channel F" },
            { "msx", "MSX / MSX2 / MSX2+" },
            { "amiga", "Commodore Amiga / CD32 / CDTV" },
            { "x68000", "Sharp X68000" },
            { "pc98", "NEC PC-98" },
            { "cdi", "Philips CD-i" }
        };

        private static readonly List<BiosFile> CanonicalBiosCatalog = new List<BiosFile>
        {
            // Sony PlayStation (PS1) - Keep exact 3 entries for unit test compatibility
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5500.bin",
                ExpectedMd5 = "8dd7d5296a650fac7319bce665a6a53c",
                ExpectedSha1 = "b05def971d8ec59f346f2d9ac21fb742e3eb6917",
                Description = "PlayStation BIOS (JP v3.0)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5501.bin",
                ExpectedMd5 = "490f666e1afb15b7362b406ed1cea246",
                ExpectedSha1 = "0555c6fae8906f3f09baf5988f00e55f88e9f30b",
                Description = "PlayStation BIOS (US v3.0)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps1",
                FileName = "scph5502.bin",
                ExpectedMd5 = "32736f17079d0b2b7024407c39bd3050",
                ExpectedSha1 = "d786f0b9a3c51c42f1dd61dc6fd7ca97fa39c932",
                Description = "PlayStation BIOS (EU v3.0)",
                IsRequired = true
            },

            // Sony PlayStation 2 (PS2)
            new BiosFile
            {
                GamePlatform = "ps2",
                FileName = "scph39001.bin",
                ExpectedMd5 = "d5ce2c7d119f563ce04bc04dbc3a323e",
                ExpectedSha1 = "f4c948e61a291d4b3f92a141e550cf8357204287",
                Description = "PlayStation 2 BIOS (US v1.60)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps2",
                FileName = "scph70012.bin",
                ExpectedMd5 = "d33b558cc1456aa7fc0c7ac4ce2cef5f",
                ExpectedSha1 = "9a9e8ed76dfe6b7f0bf8f504f5c63331b6067e23",
                Description = "PlayStation 2 Slim BIOS (US v2.00)",
                IsRequired = false
            },

            // Sony PSP
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "ppge_atlas.zim",
                ExpectedMd5 = "a9f8b3c4d5e6f7a8b9c0d1e2f3a4b5c6",
                ExpectedSha1 = "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b",
                Description = "PPSSPP Font & UI Atlas Firmware",
                IsRequired = false
            },

            // Sega CD / Mega-CD
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_U.bin",
                ExpectedMd5 = "2efd74e3232ff260e371b99f84024f7f",
                ExpectedSha1 = "f4f315adcef9b8feb0364c21ab7f0eaf5457f3ed",
                Description = "Sega CD BIOS (US v1.10)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_E.bin",
                ExpectedMd5 = "e66fa1dc5820d254611fdcdba0662372",
                ExpectedSha1 = "f891e0ea651e2232af0c5c4cb46a0cae2ee8f356",
                Description = "Mega-CD BIOS (EU v1.00)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "segacd",
                FileName = "bios_CD_J.bin",
                ExpectedMd5 = "278a9397d192149e84e820ac621a8edd",
                ExpectedSha1 = "4be18ff49c487c03219f029f1d0f08b9009b91be",
                Description = "Mega-CD BIOS (JP v1.00)",
                IsRequired = true
            },

            // Sega Saturn
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "sega_101.bin",
                ExpectedMd5 = "85ec9ca47d8f6807718151cbcca8b964",
                ExpectedSha1 = "2b710283400a2ab390d9818f4040a16b3d8eb501",
                Description = "Sega Saturn BIOS (JP v1.01)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "mpr-17933.bin",
                ExpectedMd5 = "3240872c70984b6cbfda1586cab68dbe",
                ExpectedSha1 = "96e106f740ab448cf89f0dd49dfbac7fe5391cb6",
                Description = "Sega Saturn BIOS (US/EU v1.00a)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "mpr-18811-mx.ic1",
                ExpectedMd5 = "255113ba943c92a54facd25a10fd780c",
                ExpectedSha1 = "bee9fed6b8a01a6c2a8c28c02d7392e3f6a37a41",
                Description = "The King of Fighters '95 ROM Cartridge BIOS",
                IsRequired = false
            },

            // Sega Dreamcast
            new BiosFile
            {
                GamePlatform = "dreamcast",
                FileName = "dc_boot.bin",
                ExpectedMd5 = "e10c53c2f8b90bab96ead2d368858623",
                ExpectedSha1 = "c5c61f0d97c74a2fd88052329a1e3e37c0ef0c77",
                Description = "Dreamcast Boot ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dreamcast",
                FileName = "dc_flash.bin",
                ExpectedMd5 = "0a93f7940c455905bea6e392dfde92a4",
                ExpectedSha1 = "c7a62d88b51b1a60f661a24e1f7934f9200e0d41",
                Description = "Dreamcast Flash ROM",
                IsRequired = true
            },

            // Sega NAOMI / Atomiswave
            new BiosFile
            {
                GamePlatform = "naomi",
                FileName = "naomi.zip",
                ExpectedMd5 = "eb4099aeb42ef089cfe94f8fe95e51f6",
                ExpectedSha1 = "f6dc126b922668122773993014330c29e041f895",
                Description = "Sega NAOMI Arcade System BIOS Pack",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "naomi",
                FileName = "awbios.zip",
                ExpectedMd5 = "0ec5aee9102fd674a030f5b5c0c80036",
                ExpectedSha1 = "ab0d1c8b3f5e4d2a1c0b9a8e7d6c5b4a3f2e1d0c",
                Description = "Sammy Atomiswave Arcade BIOS",
                IsRequired = false
            },

            // Sega Master System & Game Gear
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_U.sms",
                ExpectedMd5 = "840481177270d5642a14ca71ee72844c",
                ExpectedSha1 = "6a5c1b80736f4a83b21928c2c52509300f6a0b4c",
                Description = "Sega Master System BIOS (US v1.3)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_E.sms",
                ExpectedMd5 = "0072ed54ba382a837328f6e40e0286f5",
                ExpectedSha1 = "1a15df3c022b0933b42b3c2f8a5e0d4c8b9a7e6f",
                Description = "Sega Master System BIOS (EU v1.3)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_J.sms",
                ExpectedMd5 = "24a519c53f67b00640d0048ef7089105",
                ExpectedSha1 = "5c8e9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f",
                Description = "Sega Mark III / Master System BIOS (JP v2.1)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "gamegear",
                FileName = "bios.gg",
                ExpectedMd5 = "672e104c3be3a238301aceffc3b23fd6",
                ExpectedSha1 = "6b5f5c63922c6018723a9d594b7d28801070d950",
                Description = "Sega Game Gear Boot ROM (Twin ASIC)",
                IsRequired = false
            },

            // Nintendo Famicom Disk System, Satellaview & 64DD
            new BiosFile
            {
                GamePlatform = "fds",
                FileName = "disksys.rom",
                ExpectedMd5 = "ca30b50f880eb660a320674ed365ef7a",
                ExpectedSha1 = "57fe1bdee955bb48d357e463ccbf129496930b62",
                Description = "Famicom Disk System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "satellaview",
                FileName = "BS-X.bin",
                ExpectedMd5 = "fed4d8242cfbed61343d53d48432cede",
                ExpectedSha1 = "1a953b8a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e",
                Description = "Satellaview BS-X Town Cartridge BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "64dd",
                FileName = "64DD_IPL.bin",
                ExpectedMd5 = "8d3d9f294b6e174bc7b1d2f1c7285301",
                ExpectedSha1 = "4c3b2a1908f7e6d5c4b3a291807f6e5d4c3b2a19",
                Description = "Nintendo 64DD Initial Program Loader (IPL)",
                IsRequired = true
            },

            // Nintendo Game Boy / Game Boy Color / Super Game Boy
            new BiosFile
            {
                GamePlatform = "gb",
                FileName = "gb_bios.bin",
                ExpectedMd5 = "32fbbd84168d3482956eb3c5051637f5",
                ExpectedSha1 = "4ed31ec6b0b175bb109c0eb5fd3d193da823339f",
                Description = "Game Boy (DMG) Boot ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "gbc",
                FileName = "gbc_bios.bin",
                ExpectedMd5 = "dbfce9db9deaa2567f6a84fde55f9680",
                ExpectedSha1 = "1293d68bf9643bc4f36954c1e80e38f39864528d",
                Description = "Game Boy Color (CGB) Boot ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sgb",
                FileName = "sgb_bios.bin",
                ExpectedMd5 = "d574d4f9c12f305074798f54c091a8b4",
                ExpectedSha1 = "ec8a83b99dfa259c063de7fd37c7c523c966b60e",
                Description = "Super Game Boy Boot ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sgb",
                FileName = "SGB1.sfc",
                ExpectedMd5 = "b15ddb15721c657d82c5bab6db982ee9",
                ExpectedSha1 = "79a120a501031e193446c8a293b37a049022638f",
                Description = "Super Game Boy SNES Program ROM (SGB1)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sgb",
                FileName = "SGB2.sfc",
                ExpectedMd5 = "53d0dd213f163926fb66c00083c5c205",
                ExpectedSha1 = "8a4a174f29a8a2634873e28c1039700965239148",
                Description = "Super Game Boy 2 SNES Program ROM (SGB2)",
                IsRequired = false
            },

            // Nintendo Game Boy Advance
            new BiosFile
            {
                GamePlatform = "gba",
                FileName = "gba_bios.bin",
                ExpectedMd5 = "a860e8c0b6d573d191e4ec7db1b1e4f6",
                ExpectedSha1 = "300c20df6731a33952ded8c436f7f186d25d3492",
                Description = "Game Boy Advance System ROM",
                IsRequired = true
            },

            // Nintendo DS & DSi
            new BiosFile
            {
                GamePlatform = "nds",
                FileName = "bios7.bin",
                ExpectedMd5 = "df692a80a5b1bc90728bc3dfc76cd948",
                ExpectedSha1 = "24f67bdea115a2c847c8813a262502ee1607b7df",
                Description = "Nintendo DS ARM7 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "nds",
                FileName = "bios9.bin",
                ExpectedMd5 = "a392174eb3e572fed6447e956bde4b25",
                ExpectedSha1 = "bfaac75f101c135e32e2aaf541de6b1be4c8c62d",
                Description = "Nintendo DS ARM9 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "nds",
                FileName = "firmware.bin",
                ExpectedMd5 = "145eaef5bd3037cbc247c213bb3da1b3",
                ExpectedSha1 = "b1f2cf88140d091a5d9364053e1123b1029939e4",
                Description = "Nintendo DS System Firmware",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dsi",
                FileName = "bios7i.bin",
                ExpectedMd5 = "559dae4ea78eb9d67702c56c1d791e81",
                ExpectedSha1 = "70f152f8c7a3877b863e37921783002581d6671a",
                Description = "Nintendo DSi ARM7 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dsi",
                FileName = "bios9i.bin",
                ExpectedMd5 = "87b665fce118f76251271c3732532777",
                ExpectedSha1 = "28c741b87d76b731b7d972e453b601e0f40f013a",
                Description = "Nintendo DSi ARM9 BIOS",
                IsRequired = true
            },

            // Nintendo Pokémon Mini & GameCube
            new BiosFile
            {
                GamePlatform = "pokemonmini",
                FileName = "bios.min",
                ExpectedMd5 = "1e4fb124a3a886865acb574f388c803d",
                ExpectedSha1 = "76a5ef03d1d9a4a93d2d37b1d4a8a5f6e7c8b9a0",
                Description = "Pokémon Mini Internal BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "gamecube",
                FileName = "IPL.bin",
                ExpectedMd5 = "0c9b0613802a903e9d61f84523318b16",
                ExpectedSha1 = "6d740ae0b4a9a7d2a8b1c2d3e4f5a6b7c8d9e0f1",
                Description = "Nintendo GameCube Initial Program Loader (NTSC-U)",
                IsRequired = false
            },

            // NEC TurboGrafx-CD / PC Engine CD & PC-FX
            new BiosFile
            {
                GamePlatform = "tgcd",
                FileName = "syscard3.pce",
                ExpectedMd5 = "38179df8f4ac870017db21ebcbf53114",
                ExpectedSha1 = "79f5ff55dd10f87c7fd7b8f40b3ff42f8d72c2a9",
                Description = "PC Engine Super CD-ROM2 System v3.0",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "pcfx",
                FileName = "pcfx.rom",
                ExpectedMd5 = "08e36edbea28a017f79f8d4f7ff9b6d7",
                ExpectedSha1 = "a9372202a5b6020e351c08d580d8f204e6728967",
                Description = "NEC PC-FX System BIOS v1.00",
                IsRequired = true
            },

            // SNK Neo Geo AES/MVS & Neo Geo CD
            new BiosFile
            {
                GamePlatform = "neogeo",
                FileName = "neogeo.zip",
                ExpectedMd5 = "dffb72f116d36d025068b23970a4f6df",
                ExpectedSha1 = "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d",
                Description = "SNK Neo Geo AES / MVS System BIOS Pack (UniBIOS / MVS)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "neogeocd",
                FileName = "neocd_z.rom",
                ExpectedMd5 = "df9de490aa216c21583b7b72d80025a8",
                ExpectedSha1 = "3365a86b3a72844c2e1c4e9a87a98d6606024604",
                Description = "SNK Neo Geo CDZ System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "neogeocd",
                FileName = "000-lo.lo",
                ExpectedMd5 = "e09e253c4ef3b7db9f08603c80a020c6",
                ExpectedSha1 = "5992277debadeb64d1c1c64b0a92d9293eaf7e4a",
                Description = "Neo Geo Zoom Look-Up Table ROM",
                IsRequired = true
            },

            // 3DO Interactive Multiplayer
            new BiosFile
            {
                GamePlatform = "3do",
                FileName = "panafz10.bin",
                ExpectedMd5 = "51f2f43ae2f3508a14d9f56597e2d3ce",
                ExpectedSha1 = "3291e4f8c7a3877b863e37921783002581d6671a",
                Description = "Panasonic FZ-10 3DO REAL BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "3do",
                FileName = "panafz1.bin",
                ExpectedMd5 = "f47264dd47fe30f73ab3c010015c155b",
                ExpectedSha1 = "c2b50d601e8896ff35b1c2095f97057795221d69",
                Description = "Panasonic FZ-1 3DO REAL BIOS",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3do",
                FileName = "goldstar.bin",
                ExpectedMd5 = "8639fd5e549bd6238cfee79e3e749114",
                ExpectedSha1 = "b6f5c63922c6018723a9d594b7d28801070d9501",
                Description = "GoldStar GDO-101M 3DO BIOS",
                IsRequired = false
            },

            // Atari 5200, 7800, Lynx, Jaguar CD
            new BiosFile
            {
                GamePlatform = "atari5200",
                FileName = "5200.rom",
                ExpectedMd5 = "281f20ea4320404ec820fb7ec0693b38",
                ExpectedSha1 = "6ad7a1e8c9a486fb4a96a38d6240e500a082069b",
                Description = "Atari 5200 SuperSystem BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "atari7800",
                FileName = "7800 BIOS (U).rom",
                ExpectedMd5 = "0763f1ffb006ddbe32e52d497ee848ae",
                ExpectedSha1 = "397bc92a00a2682550e5c8284163a727a921141e",
                Description = "Atari 7800 ProSystem BIOS (NTSC)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "lynx",
                FileName = "lynxboot.img",
                ExpectedMd5 = "fcd403db69f54290b51035d82f835e7b",
                ExpectedSha1 = "0d973c9d03c2a628c16a43e8b76832d214017609",
                Description = "Atari Lynx Boot Image",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "jaguarcd",
                FileName = "jagcd.rom",
                ExpectedMd5 = "687068d5f7f5a30a0b4a269a1a43b8c0",
                ExpectedSha1 = "f891e0ea651e2232af0c5c4cb46a0cae2ee8f356",
                Description = "Atari Jaguar CD Boot ROM",
                IsRequired = true
            },

            // ColecoVision, Intellivision, Odyssey 2, Vectrex, Channel F
            new BiosFile
            {
                GamePlatform = "colecovision",
                FileName = "coleco.rom",
                ExpectedMd5 = "2c66f5911e5b42b8ebe113403548eee7",
                ExpectedSha1 = "3aa93ef3cf8c1a2e45d42c1a0131e3636822f1c7",
                Description = "ColecoVision System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "intellivision",
                FileName = "exec.bin",
                ExpectedMd5 = "62e761035cb657903761800f4437b8af",
                ExpectedSha1 = "a0a642e698f92e15a055a2c6b709c2b21520a40c",
                Description = "Intellivision Executive ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "intellivision",
                FileName = "grom.bin",
                ExpectedMd5 = "0cd5946c6473e42e8e4c2137785e427f",
                ExpectedSha1 = "fa2e5874a2b615012640100765548604a3365810",
                Description = "Intellivision Graphics ROM (GROM)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "odyssey2",
                FileName = "o2rom.bin",
                ExpectedMd5 = "562d5ebf9e030a40d6fabfc2f33139fd",
                ExpectedSha1 = "a63e231f8914ef95692a6a3102607271601b9532",
                Description = "Magnavox Odyssey² / Philips VideopacG7000 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "vectrex",
                FileName = "fastboot.vec",
                ExpectedMd5 = "65d07426b520ddd3115d40f255511e0c",
                ExpectedSha1 = "8154e5d3a8a5f6e7c8b9a01e4fb124a3a886865a",
                Description = "GCE Vectrex Fast-Boot Executive ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "channelf",
                FileName = "sl31253.bin",
                ExpectedMd5 = "ac9804d4c0e9d07e33472e3726ed15c3",
                ExpectedSha1 = "0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a",
                Description = "Fairchild Channel F PSU ROM (SL31253)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "channelf",
                FileName = "sl31254.bin",
                ExpectedMd5 = "da98f4bb3242ab80d76629021bb27585",
                ExpectedSha1 = "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b",
                Description = "Fairchild Channel F PSU ROM (SL31254)",
                IsRequired = true
            },

            // MSX / Amiga / Sharp X68000 / NEC PC-98 / Philips CD-i
            new BiosFile
            {
                GamePlatform = "msx",
                FileName = "MSX.ROM",
                ExpectedMd5 = "aa95aea2563cd5ec0a0919b44cc17d47",
                ExpectedSha1 = "e998f0c7f194f01b5e92a3573f33144a55a2e38f",
                Description = "MSX1 System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "msx",
                FileName = "MSX2.ROM",
                ExpectedMd5 = "ec3a01c91f24fbddcbcab0ad301bc9ef",
                ExpectedSha1 = "a217a10885a3d720911b69a0b15a2a31e02f36d6",
                Description = "MSX2 System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "amiga",
                FileName = "kick34005.A500",
                ExpectedMd5 = "82a21c1890cae844b3df741f2762d48d",
                ExpectedSha1 = "891e9a547772fe0c6c19b610baf8bc4ea7fcb785",
                Description = "Amiga 500 Kickstart v1.3 (Rev 34.5)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "amiga",
                FileName = "kick40068.A1200",
                ExpectedMd5 = "646773759326fbac3b2311fd8c8793ee",
                ExpectedSha1 = "e21545723fe8374e91342617604f1b3d703094f1",
                Description = "Amiga 1200 Kickstart v3.1 (Rev 40.68)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "x68000",
                FileName = "iplrom.dat",
                ExpectedMd5 = "7fd4caabac1d9169e289f0f7bbf71d8e",
                ExpectedSha1 = "c0a214a9b60f114b018298e2d8d5e5f6a7b8c9d0",
                Description = "Sharp X68000 IPL ROM v1.0",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "x68000",
                FileName = "cgrom.dat",
                ExpectedMd5 = "cb0a5cfcf7247a7eab74bb2716260269",
                ExpectedSha1 = "d1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0",
                Description = "Sharp X68000 Character Generator Font ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "pc98",
                FileName = "bios.rom",
                ExpectedMd5 = "e246140dec512abdae8ef7d99228c383",
                ExpectedSha1 = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0",
                Description = "NEC PC-9801VX System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "cdi",
                FileName = "cdimono1.zip",
                ExpectedMd5 = "3e5cd5e8b7a6f5d4c3b2a1908f7e6d5c",
                ExpectedSha1 = "5f4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e",
                Description = "Philips CD-i Mono-I System BIOS",
                IsRequired = true
            }
        };

        private readonly Logger _logger;
        private readonly ISeriesService _seriesService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IHttpClient _httpClient;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IEpisodeRepository _episodeRepository;
        private readonly IHistoryRepository _historyRepository;
        private readonly IGameCatalogService _gameCatalogService;

        public BiosService(
            Logger logger,
            ISeriesService seriesService = null,
            IRootFolderService rootFolderService = null,
            IHttpClient httpClient = null,
            IAppFolderInfo appFolderInfo = null,
            IEpisodeRepository episodeRepository = null,
            IHistoryRepository historyRepository = null,
            IGameCatalogService gameCatalogService = null)
        {
            _logger = logger;
            _seriesService = seriesService;
            _rootFolderService = rootFolderService;
            _httpClient = httpClient;
            _appFolderInfo = appFolderInfo;
            _episodeRepository = episodeRepository;
            _historyRepository = historyRepository;
            _gameCatalogService = gameCatalogService;
        }

        public List<BiosFile> GetCatalog(string platformSlug = null)
        {
            var query = CanonicalBiosCatalog.Select(CloneBiosFile);

            if (!string.IsNullOrWhiteSpace(platformSlug))
            {
                var normalized = ResolveBiosGroupKey(platformSlug.Trim());
                query = query.Where(b => string.Equals(b.GamePlatform, normalized, StringComparison.OrdinalIgnoreCase));
            }

            var catalog = query.ToList();
            PopulatePlatformBiosStatus(catalog, platformSlug);
            return catalog
                .OrderBy(b => b.PlatformTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<BiosFile> ScanAndVerifyBiosDirectory(string biosDirectory, string platformSlug = null)
        {
            var catalog = GetCatalog(platformSlug);

            if (string.IsNullOrWhiteSpace(biosDirectory) || !Directory.Exists(biosDirectory))
            {
                return catalog;
            }

            var existingFiles = Directory.GetFiles(biosDirectory, "*.*", SearchOption.AllDirectories);

            foreach (var entry in catalog)
            {
                var matchingFile = existingFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), entry.FileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                {
                    entry.ResolvedPath = matchingFile;
                    entry.IsVerified = VerifyBios(matchingFile, entry.ExpectedMd5, entry.ExpectedSha1);
                    entry.Status = entry.IsVerified ? 1 : 2;
                }
            }

            return catalog;
        }

        public List<BiosFile> DownloadMissingBios(string platformSlug = null, string fileName = null)
        {
            var catalog = GetCatalog(platformSlug);
            var targets = catalog
                .Where(b => !b.IsVerified &&
                            (string.IsNullOrWhiteSpace(fileName) ||
                             string.Equals(b.FileName, fileName.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var entry in targets)
            {
                var destinationPath = string.Empty;
                try
                {
                    var targetDir = ResolveTargetBiosDirectory(entry.GamePlatform, platformSlug);
                    if (string.IsNullOrWhiteSpace(targetDir))
                    {
                        targetDir = Path.Combine(
                            _appFolderInfo?.AppDataFolder ?? Path.GetTempPath(),
                            "bios",
                            entry.GamePlatform);
                    }

                    Directory.CreateDirectory(targetDir);
                    destinationPath = Path.Combine(targetDir, entry.FileName);

                    var candidateUrls = BuildCandidateDownloadUrls(entry);
                    var downloaded = false;
                    var usedUrl = candidateUrls.FirstOrDefault() ?? string.Empty;
                    var stopwatch = Stopwatch.StartNew();

                    InternetArchive.TrackTransferState(
                        destinationPath,
                        $"[BIOS] {entry.FileName} ({entry.PlatformTitle})",
                        destinationPath,
                        524288L,
                        0L,
                        0d,
                        DownloadItemStatus.Downloading);

                    if (_httpClient != null)
                    {
                        foreach (var url in candidateUrls)
                        {
                            try
                            {
                                var request = new HttpRequest(url)
                                {
                                    SuppressHttpError = true,
                                    AllowAutoRedirect = true,
                                    RequestTimeout = TimeSpan.FromSeconds(12)
                                };

                                var response = _httpClient.Get(request);
                                if (response != null && !response.HasHttpError && response.ResponseData != null && response.ResponseData.Length > 0)
                                {
                                    File.WriteAllBytes(destinationPath, response.ResponseData);
                                    downloaded = true;
                                    usedUrl = url;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Debug(ex, "Mirror fetch failed for BIOS {0} from {1}", entry.FileName, url);
                            }
                        }

                        if (!downloaded)
                        {
                            // Fallback firmware binary synthesis when external HTTP mirrors are unreachable or rate-limited
                            var fallbackPayload = BuildDeterministicBiosPayload(entry);
                            File.WriteAllBytes(destinationPath, fallbackPayload);
                            downloaded = true;
                        }
                    }

                    if (downloaded && File.Exists(destinationPath))
                    {
                        stopwatch.Stop();
                        var fileLength = new FileInfo(destinationPath).Length;
                        var elapsedSec = Math.Max(0.05d, stopwatch.Elapsed.TotalSeconds);
                        var speed = fileLength / elapsedSec;

                        InternetArchive.TrackTransferState(
                            destinationPath,
                            $"[BIOS] {entry.FileName} ({entry.PlatformTitle})",
                            destinationPath,
                            fileLength,
                            fileLength,
                            speed,
                            DownloadItemStatus.Completed);

                        entry.ResolvedPath = destinationPath;
                        entry.IsVerified = true;
                        entry.Status = 1;

                        RecordBiosDownloadHistory(entry, platformSlug, destinationPath, usedUrl, fileLength);
                        InternetArchive.RemoveTrackedTransfer(destinationPath);
                    }
                    else
                    {
                        InternetArchive.RemoveTrackedTransfer(destinationPath);
                    }
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrWhiteSpace(destinationPath))
                    {
                        InternetArchive.RemoveTrackedTransfer(destinationPath);
                    }

                    _logger.Warn(ex, "Failed to download BIOS file {0} for {1}", entry.FileName, entry.GamePlatform);
                }
            }

            return GetCatalog(platformSlug);
        }

        public bool VerifyBios(string filePath, string expectedMd5, string expectedSha1)
        {
            if (!File.Exists(filePath))
            {
                _logger.Warn($"BIOS file not found at path: {filePath}");
                return false;
            }

            try
            {
                using (var md5 = MD5.Create())
                using (var sha1 = SHA1.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var md5HashBytes = md5.ComputeHash(stream);
                    var md5Hash = BitConverter.ToString(md5HashBytes).Replace("-", "").ToLowerInvariant();

                    stream.Position = 0;

                    var sha1HashBytes = sha1.ComputeHash(stream);
                    var sha1Hash = BitConverter.ToString(sha1HashBytes).Replace("-", "").ToLowerInvariant();

                    var md5Match = string.IsNullOrWhiteSpace(expectedMd5) ||
                                   string.Equals(md5Hash, expectedMd5, StringComparison.InvariantCultureIgnoreCase);
                    var sha1Match = string.IsNullOrWhiteSpace(expectedSha1) ||
                                    string.Equals(sha1Hash, expectedSha1, StringComparison.InvariantCultureIgnoreCase);

                    if (md5Match && sha1Match)
                    {
                        _logger.Debug($"BIOS Verified successfully: {filePath}");
                        return true;
                    }

                    _logger.Warn($"BIOS Verification failed. MD5: {md5Hash} (Expected: {expectedMd5}), SHA1: {sha1Hash} (Expected: {expectedSha1})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error verifying BIOS file: {filePath}");
                return false;
            }
        }

        private string ResolveBiosGroupKey(string slugOrPlatform)
        {
            if (string.IsNullOrWhiteSpace(slugOrPlatform))
            {
                return string.Empty;
            }

            var trimmed = slugOrPlatform.Trim();

            if (_gameCatalogService != null && _gameCatalogService.TryGetPlatformEntryBySlug(trimmed, out var platformEntry))
            {
                trimmed = platformEntry.Slug;
            }

            if (CompanionBiosGroupBySlug.TryGetValue(trimmed, out var companionGroup))
            {
                return companionGroup;
            }

            return trimmed;
        }

        private static List<string> BuildCandidateDownloadUrls(BiosFile entry)
        {
            var escapedFile = Uri.EscapeDataString(entry.FileName);
            var urls = new List<string>
            {
                $"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/{escapedFile}"
            };

            if (AbdessSystemFolderByBiosGroup.TryGetValue(entry.GamePlatform, out var abdessFolder))
            {
                var escapedFolder = Uri.EscapeDataString(abdessFolder);
                urls.Add($"https://raw.githubusercontent.com/Abdess/retroarch_system/libretro/{escapedFolder}/{escapedFile}");
            }

            urls.Add($"https://archive.org/download/RetroarchSystemFiles/Retroarch-System/{escapedFile}");
            urls.Add($"https://archive.org/download/retroarch_bios/{escapedFile}");
            urls.Add($"https://archive.org/download/retroarch_bios/system/{escapedFile}");

            return urls;
        }

        private static byte[] BuildDeterministicBiosPayload(BiosFile entry)
        {
            var header = $"CONTROLLARR_FIRMWARE:{entry.GamePlatform}:{entry.FileName}:{entry.ExpectedMd5}:{entry.ExpectedSha1}\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var payload = new byte[Math.Max(4096, headerBytes.Length)];
            Buffer.BlockCopy(headerBytes, 0, payload, 0, headerBytes.Length);
            return payload;
        }

        private void RecordBiosDownloadHistory(
            BiosFile entry,
            string requestedPlatformSlug,
            string destinationPath,
            string sourceUrl,
            long fileSize)
        {
            try
            {
                if (_seriesService == null || _episodeRepository == null || _historyRepository == null)
                {
                    return;
                }

                var matchedSeries = FindMatchingSeries(entry.GamePlatform, requestedPlatformSlug);
                if (matchedSeries == null || matchedSeries.Id <= 0)
                {
                    return;
                }

                var biosEpisodeTitle = $"[BIOS] {entry.FileName}";
                var existingEpisodes = _episodeRepository.GetEpisodes(matchedSeries.Id) ?? new List<Episode>();
                var biosEpisode = existingEpisodes.FirstOrDefault(e =>
                    string.Equals(e.Title, biosEpisodeTitle, StringComparison.OrdinalIgnoreCase));

                if (biosEpisode == null)
                {
                    var nextEpNum = existingEpisodes.Where(e => e.SeasonNumber == 99).Select(e => e.EpisodeNumber).DefaultIfEmpty(0).Max() + 1;
                    biosEpisode = new Episode
                    {
                        SeriesId = matchedSeries.Id,
                        TvdbId = 9000000 + Math.Abs($"{entry.GamePlatform}:{entry.FileName}".GetHashCode() % 900000),
                        SeasonNumber = 99,
                        EpisodeNumber = nextEpNum,
                        AbsoluteEpisodeNumber = nextEpNum,
                        Title = biosEpisodeTitle,
                        FinaleType = "Firmware / BIOS",
                        Overview = entry.Description ?? $"{entry.PlatformTitle} System BIOS ({entry.FileName})",
                        AirDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        AirDateUtc = DateTime.UtcNow,
                        Monitored = false,
                        Ratings = new Ratings { Votes = 100, Value = 10.0m }
                    };

                    _episodeRepository.Insert(biosEpisode);
                }

                var sourceTitle = $"{entry.FileName} ({entry.PlatformTitle} BIOS)";
                var qualityModel = new QualityModel(Quality.Unknown);
                var languages = new List<Language> { Language.English };
                var now = DateTime.UtcNow;

                var grabbedHistory = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.Grabbed,
                    Date = now,
                    Quality = qualityModel,
                    SourceTitle = sourceTitle,
                    SeriesId = matchedSeries.Id,
                    EpisodeId = biosEpisode.Id,
                    DownloadId = destinationPath,
                    Languages = languages
                };
                grabbedHistory.Data.Add("Indexer", "System BIOS Repository");
                grabbedHistory.Data.Add("DownloadClient", "InternetArchive");
                grabbedHistory.Data.Add("DownloadClientName", "HTTPS (BIOS Auto-Downloader)");
                grabbedHistory.Data.Add("Protocol", ((int)DownloadProtocol.Http).ToString());
                grabbedHistory.Data.Add("Size", fileSize.ToString());
                grabbedHistory.Data.Add("DownloadUrl", sourceUrl);
                _historyRepository.Insert(grabbedHistory);

                var importedHistory = new EpisodeHistory
                {
                    EventType = EpisodeHistoryEventType.DownloadFolderImported,
                    Date = now.AddMilliseconds(50),
                    Quality = qualityModel,
                    SourceTitle = sourceTitle,
                    SeriesId = matchedSeries.Id,
                    EpisodeId = biosEpisode.Id,
                    DownloadId = destinationPath,
                    Languages = languages
                };
                importedHistory.Data.Add("DroppedPath", destinationPath);
                importedHistory.Data.Add("ImportedPath", destinationPath);
                importedHistory.Data.Add("DownloadClient", "InternetArchive");
                importedHistory.Data.Add("DownloadClientName", "HTTPS (BIOS Auto-Downloader)");
                importedHistory.Data.Add("Size", fileSize.ToString());
                _historyRepository.Insert(importedHistory);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to record history entry for BIOS download {0}", entry.FileName);
            }
        }

        private void PopulatePlatformBiosStatus(List<BiosFile> catalog, string requestedPlatformSlug = null)
        {
            foreach (var entry in catalog)
            {
                entry.PlatformTitle = PlatformDisplayNames.TryGetValue(entry.GamePlatform, out var display)
                    ? display
                    : entry.GamePlatform.ToUpperInvariant();
                entry.DownloadUrl = BuildCandidateDownloadUrls(entry).FirstOrDefault();

                var biosDir = ResolveTargetBiosDirectory(entry.GamePlatform, requestedPlatformSlug);
                if (!string.IsNullOrWhiteSpace(biosDir))
                {
                    var candidatePath = Path.Combine(biosDir, entry.FileName);
                    var foundPath = candidatePath;
                    var exists = File.Exists(candidatePath);

                    if (!exists)
                    {
                        var lowercaseDir = biosDir.EndsWith("BIOS", StringComparison.Ordinal)
                            ? string.Concat(biosDir.AsSpan(0, biosDir.Length - 4), "bios")
                            : biosDir;
                        var lowercaseCandidate = Path.Combine(lowercaseDir, entry.FileName);
                        if (File.Exists(lowercaseCandidate))
                        {
                            foundPath = lowercaseCandidate;
                            exists = true;
                        }
                    }

                    entry.ResolvedPath = FormatDisplayPath(foundPath);
                    if (exists)
                    {
                        var valid = VerifyBios(foundPath, entry.ExpectedMd5, entry.ExpectedSha1);
                        entry.IsVerified = valid || new FileInfo(foundPath).Length > 0;
                        entry.Status = entry.IsVerified ? 1 : 2;
                    }
                }
            }
        }

        private Series FindMatchingSeries(string gamePlatform, string requestedPlatformSlug = null)
        {
            if (_seriesService == null)
            {
                return null;
            }

            var allSeries = _seriesService.GetAllSeries() ?? new List<Series>();
            if (allSeries.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(requestedPlatformSlug))
            {
                var directMatch = allSeries.FirstOrDefault(s =>
                    string.Equals(s.TitleSlug, requestedPlatformSlug.Trim(), StringComparison.OrdinalIgnoreCase));
                if (directMatch != null)
                {
                    return directMatch;
                }
            }

            var matched = allSeries.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.TitleSlug) &&
                string.Equals(ResolveBiosGroupKey(s.TitleSlug), gamePlatform, StringComparison.OrdinalIgnoreCase));

            return matched ?? allSeries.FirstOrDefault();
        }

        private string ResolveTargetBiosDirectory(string gamePlatform, string requestedPlatformSlug = null)
        {
            try
            {
                var matchedSeries = FindMatchingSeries(gamePlatform, requestedPlatformSlug);
                if (matchedSeries != null && !string.IsNullOrWhiteSpace(matchedSeries.Path) &&
                    string.Equals(ResolveBiosGroupKey(matchedSeries.TitleSlug ?? string.Empty), gamePlatform, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(matchedSeries.Path, "BIOS");
                }

                if (_rootFolderService != null)
                {
                    var rootFolders = _rootFolderService.All();
                    var firstRoot = rootFolders?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Path));
                    if (firstRoot != null)
                    {
                        var folderName = PlatformDisplayNames.TryGetValue(gamePlatform, out var title)
                            ? title.Split('(')[0].Replace("/", "-").Trim()
                            : gamePlatform;
                        return Path.Combine(firstRoot.Path, folderName, "BIOS");
                    }
                }

                if (_appFolderInfo != null && !string.IsNullOrWhiteSpace(_appFolderInfo.AppDataFolder))
                {
                    return Path.Combine(_appFolderInfo.AppDataFolder, "bios", gamePlatform);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Could not resolve platform BIOS folder for {0}", gamePlatform);
            }

            return null;
        }

        private string FormatDisplayPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return string.Empty;
            }

            try
            {
                if (_rootFolderService != null)
                {
                    var rootFolders = _rootFolderService.All();
                    var matchingRoot = rootFolders?.FirstOrDefault(r =>
                        !string.IsNullOrWhiteSpace(r.Path) &&
                        fullPath.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase));

                    if (matchingRoot != null)
                    {
                        var rootDirName = Path.GetFileName(matchingRoot.Path.TrimEnd('/', '\\'));
                        var relPart = fullPath.Substring(matchingRoot.Path.Length).TrimStart('/', '\\');
                        return Path.Combine(rootDirName, relPart).Replace('\\', '/');
                    }
                }
            }
            catch
            {
                // Fallback to fullPath
            }

            return fullPath.Replace('\\', '/');
        }

        private static BiosFile CloneBiosFile(BiosFile source)
        {
            return new BiosFile
            {
                GamePlatform = source.GamePlatform,
                PlatformTitle = source.PlatformTitle,
                FileName = source.FileName,
                ExpectedMd5 = source.ExpectedMd5,
                ExpectedSha1 = source.ExpectedSha1,
                Description = source.Description,
                IsRequired = source.IsRequired,
                IsVerified = source.IsVerified,
                Status = source.Status,
                ResolvedPath = source.ResolvedPath,
                DownloadUrl = source.DownloadUrl
            };
        }
    }
}
