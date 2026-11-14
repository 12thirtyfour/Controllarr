using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
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
        bool IsKnownBiosFileName(string fileName);
        bool TryGetBiosFile(string fileName, out BiosFile biosFile, string platformSlug = null);
        bool ProcessCandidateBiosFile(string sourceFilePath, Series series, bool move = false);
        void ScanAndVerifyPlatformBios(Series series);
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
            { "psp", "Sony - PlayStation Portable (PPSSPP)" },
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
            { "gamecube", "Nintendo - GameCube - Wii" },
            { "tgcd", "NEC - PC Engine - TurboGrafx 16 - SuperGrafx" },
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

        private static readonly Dictionary<string, (string Md5, string Sha1)[]> AlternateBiosHashesByFileName = new Dictionary<string, (string Md5, string Sha1)[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "bios_CD_J.bin",
                new[]
                {
                    ("278a9397d192149e84e820ac621a8edd", "4846f448160059a7da0215a5df12ca160f26dd69"), // Mega-CD JP v1.00P (MPR-14088E)
                    ("9d2da8f221cdb060c552eb84f8f2892f", "f2bc9fc7ef0dc8fb2cd1fd0bd0fa2ee5ec2bf7a1"), // Mega-CD JP v1.00P (Byte-Swapped / RetroPieBIOS)
                    ("bdeb4c47da613946d422d97d98b21cda", "e16c2bc7fb1bb0fb8c21b0c9b1b11f03b25a9f4a"), // Mega-CD JP v1.00S (MPR-14088H)
                    ("b5ff2623c23c22c2b21c2a0c8b0f0a4b", "f39b11a084152a567d2d9b8bf222bb2f10bd416f"), // Mega-CD JP v1.00G (MPR-14088B)
                    ("a83f3e8c0a2a9a7d4a5a0d8d811d3055", "2fb1238cb1fb10d0fb74cd69ba8e2d83f444c22d"), // Mega-CD JP v1.00L (MPR-14088C)
                    ("2b19972f86b42070a2397101f439224b", "c29b7efdf6b910ba6bf1fd7cf9b4a0f8f4e2a5c3"), // Mega-CD JP v1.00O (MPR-14088D)
                    ("dd6cc972f9a5b58a02a5b62e1111e02f", "60e0e7de59a76a0b6c4a48ca0c90903b07b09d45")  // Mega-CD 2 JP v1.01
                }
            },
            {
                "bios_CD_U.bin",
                new[]
                {
                    ("2efd74e3232ff260e371b99f84024f7f", "f4f315adcef9b8feb0364c21ab7f0eaf5457f3ed"), // Sega CD US v1.10 (Model 1)
                    ("854b9150240a198070150e4566ae1290", "5c5eeb8ef00926bf040d1b069f904182a3b6fc2c"), // Sega CD US v2.00 (Model 2)
                    ("e623ddc4f0000108a92d92b73e15f80f", "3b19c530b1fa3441b7f7e7aa2b9b24e3c5f9b86b")  // Sega CD US v2.00W (Model 2)
                }
            },
            {
                "bios_CD_E.bin",
                new[]
                {
                    ("e66fa1dc5820d254611fdcdba0662372", "f891e0ea651e2232af0c5c4cb46a0cae2ee8f356"), // Mega-CD EU v1.00 (Model 1)
                    ("b639a2b86d7844990517a78e8a6c0f8b", "5ac310a70f6601e0f40f6e2b7984b68190d0a1a5"), // Mega-CD 2 EU v2.00 (Model 2)
                    ("529ac15a0bcd026839ed599e1a052267", "517cb8c9bf8b6d0d5a424fb3f601d83923e8842a")  // Mega-CD 2 EU v2.00W (Model 2)
                }
            },
            {
                "scph39001.bin",
                new[]
                {
                    ("d5ce2c7d119f563ce04bc04dbc3a323e", "f9a5d629a036b99128f7cb530c6e3ca016e9c8b7"),
                    ("d5ce2c7d119f563ce04bc04dbc3a323e", "f4c948e61a291d4b3f92a141e550cf8357204287")
                }
            },
            {
                "scph70012.bin",
                new[]
                {
                    ("d333558cc14561c1fdc334c75d5f37b7", "7a62e5f48603582707e9898eb055ea3eaee50d4c"),
                    ("d33b558cc1456aa7fc0c7ac4ce2cef5f", "9a9e8ed76dfe6b7f0bf8f504f5c63331b6067e23")
                }
            },
            {
                "ppge_atlas.zim",
                new[]
                {
                    ("866855cc330b9b95cc69135fb7b41d38", "30a33f3651f8e1b0967a5dfe6ff714baebcce41a"),
                    ("a93fc411c1ce7d001a2a812643c70085", "02dcd1d5928c13f0305f3af356f935fee11debde")
                }
            },
            {
                "bios_E.sms",
                new[]
                {
                    ("840481177270d5642a14ca71ee72844c", "c315672807d8ddb8d91443729405c766dd95cae7"),
                    ("e8b26871629b938887757a64798df6dc", "3af7b66248d34eb26da40c92bf2fa4c73a46a051"),
                    ("0072ed54ba382a837328f6e40e0286f5", string.Empty)
                }
            },
            {
                "SGB2.sfc",
                new[]
                {
                    ("8ecd73eb4edf7ed7e81aef1be80031d5", "e5b2922ca137051059e4269b236d07a22c07bc84"),
                    ("53d0dd213f163926fb66c00083c5c205", "8a4a174f29a8a2634873e28c1039700965239148")
                }
            },
            {
                "firmware.bin",
                new[]
                {
                    ("e45033d9b0fa6b0de071292bba7c9d13", "cfe072921ee3fb93f688743f8beef89043c3e9ad"),
                    ("145eaef5bd3037cbc247c213bb3da1b3", "b1f2cf88140d091a5d9364053e1123b1029939e4"),
                    ("94bc5094607c5e6598d50472c52f27f2", string.Empty)
                }
            },
            {
                "IPL.bin",
                new[]
                {
                    ("fae2b558ffc344467170520d62177e5c", "015808f637a984acde6a06efa7546e278293c6ee"),
                    ("b17148254a5799684c7d783206504926", "ef9194ab4804aa0aa8540d846caf291b28331165"),
                    ("0c9b0613802a903e9d61f84523318b16", string.Empty)
                }
            },
            {
                "neogeo.zip",
                new[]
                {
                    ("00dad01abdbf8ea9e79ad2fe11bdb182", "deb62b0074b8cae4f162c257662136733cfc76ad"),
                    ("dffb72f116d36d025068b23970a4f6df", string.Empty),
                    ("fc7599f3f871578fe9a0453662d1c966", "5992277debadeb64d1c1c64b0a92d9293eaf7e4a"), // Inner 000-lo.lo
                    ("e09e253c4ef3b7db9f08603c80a020c6", "5992277debadeb64d1c1c64b0a92d9293eaf7e4a")
                }
            },
            {
                "naomi.zip",
                new[]
                {
                    ("eb4099aeb42ef089cfe94f8fe95e51f6", "d7ef86bd03de7c1d0e2b0762e04b6f8f8d26dbdb"),
                    ("526eda1e2a7920c92c88178789d71d84", "c96711c01c0158f161791d6fbe75d88329e8ac0a"),
                    ("d1e4be4862f1f9592b17a042abc5831e", "91424d481ff99a8d3f4c45cea6d3f0eada049a6d")  // Inner epr-21576h.ic27
                }
            },
            {
                "awbios.zip",
                new[]
                {
                    ("85254fbe320ca82a768ec2c26bb08def", "7940c7bf29eee85a5b2fdec78750b19aa22895dc"),
                    ("0ec5aee9102fd674a030f5b5c0c80036", string.Empty),
                    ("a95530c35115323ce80d025a036ef5d5", "b4c1a26bc8906d5275eb28c701dff2b9365bcdfa")  // Inner bios0.ic23
                }
            },
            {
                "cdimono1.zip",
                new[]
                {
                    ("3c96160075816b4916f5329b5e539ca0", "36771315484a1f57227e23278df91b84b5d7d3be"),
                    ("2969341396aa61e0143dc2351aaa6ef6", "d961de803c89b3d1902d656ceb9ce7c02dccb40a")  // Inner cdi200.rom
                }
            },
            {
                "neocd_z.rom",
                new[]
                {
                    ("11526d58d4c524daef7d5d677dc6b004", "b0f1c4fa8d4492a04431805f6537138b842b549f"),
                    ("df9de490aa216c21583b7b72d80025a8", "3365a86b3a72844c2e1c4e9a87a98d6606024604")
                }
            },
            {
                "000-lo.lo",
                new[]
                {
                    ("fc7599f3f871578fe9a0453662d1c966", "5992277debadeb64d1c1c64b0a92d9293eaf7e4a"),
                    ("e09e253c4ef3b7db9f08603c80a020c6", "5992277debadeb64d1c1c64b0a92d9293eaf7e4a")
                }
            },
            {
                "jagcd.rom",
                new[]
                {
                    ("77cd95c7ad06a39f4c59995094aa10f9", "73883e7a6e9b132452436f7ab1aeaeb0776428e5"),
                    ("687068d5f7f5a30a0b4a269a1a43b8c0", string.Empty)
                }
            },
            {
                "fastboot.vec",
                new[]
                {
                    ("ba9260bb961d60e718378cc16a8051d7", "b9bbf5bb0eac52d039a4a993a2d8064b862c9e28"),
                    ("65d07426b520ddd3115d40f255511e0c", string.Empty)
                }
            },
            {
                "bios.rom",
                new[]
                {
                    ("cd237e16e7e77c06bb58540e9e9fca68", "76a7360502fa0296ea93b4c537174610a834d367"),
                    ("e246140dec512abdae8ef7d99228c383", string.Empty)
                }
            },
            {
                "PS3UPDAT.PUP",
                new[]
                {
                    ("a2b60b6ac1d2e49e230144345616927c", string.Empty), // PS3 Firmware 4.93
                    ("c179ad173bbc08b55431d30947725a4b", string.Empty), // PS3 Firmware 4.92
                    ("3694eb3fb8d9915c112e6ab41a60c69f", string.Empty), // PS3 Firmware 4.91
                    ("05fe32f5dc8c78acbcd84d36ee7fdc5b", string.Empty), // PS3 Firmware 4.90
                    ("95307e1b51d3bcc33a274db91488d29f", string.Empty), // PS3 Firmware 4.89
                    ("a0b63a3e4ae92ed176d6b9a67ce447f0", string.Empty), // PS3 Firmware 4.88
                    ("03373a581934f0d2b796156d2fb28b39", string.Empty), // PS3 Firmware 4.87
                    ("cf9cb4ba53a83ad557501417837c8de9", string.Empty), // PS3 Firmware 4.86
                    ("c975768e5d70e105a72656f498cc9be9", string.Empty), // PS3 Firmware 4.85
                    ("da485a99e39105b2ccffa5af59f82221", string.Empty), // PS3 Firmware 4.84
                    ("69b4529fd584523cf37e6d29107b9a8b", string.Empty), // PS3 Firmware 4.83
                    ("152d950c365ede4130c53ceb18dcd43b", string.Empty), // PS3 Firmware 4.82
                    ("b2483b4abc157494447c9c409c036041", string.Empty), // PS3 Firmware 4.81
                    ("2c7ac82e02db9ce396618e6644a8c52d", string.Empty), // PS3 Firmware 4.80
                    ("fd767e12d7a9f8288c277ee00704256c", string.Empty), // PS3 Firmware 4.78
                    ("bf0563dc7d3450945d080a15a64d876d", string.Empty), // PS3 Firmware 4.76
                    ("02c72e559533abde0add6850aadcfb34", string.Empty), // PS3 Firmware 4.75
                    ("6e858cb2037a6db8fdfb0b04665256f1", string.Empty), // PS3 Firmware 4.70
                    ("eb6e53673d92c9791cc9be64b0411378", string.Empty), // PS3 Firmware 4.66
                    ("8c5c36958ba5faacdc6e477797c051b8", string.Empty), // PS3 Firmware 4.65
                    ("0bbfec941b00ca88fdc777182ff95dfe", string.Empty), // PS3 Firmware 4.60
                    ("15048b78cd1f7635ef0796fe42727ce2", string.Empty), // PS3 Firmware 4.55
                    ("6edd8792c2659102026003bc8a93534e", string.Empty), // PS3 Firmware 4.53
                    ("e3decaf8beef0abc8f7999f66de05af9", string.Empty), // PS3 Firmware 4.50
                    ("6085b02c415ec912fab4670d7957db68", string.Empty), // PS3 Firmware 4.46
                    ("e6d82c8201d5d528095221bb7034ffa7", string.Empty), // PS3 Firmware 4.45
                    ("a2fba1bf2362f1b41737a69dd2433f00", string.Empty), // PS3 Firmware 4.41
                    ("1b6a1cb5a909325a7f5ed949e8cc57cb", string.Empty), // PS3 Firmware 4.40
                    ("70067a427e003e892ef81a0fca04eb1d", string.Empty), // PS3 Firmware 4.31
                    ("7e99b978582026df83ad7224ffa8c8d0", string.Empty), // PS3 Firmware 4.30
                    ("a35b7e74015408e463d15fd66a245929", string.Empty), // PS3 Firmware 4.25
                    ("31f890bd9e75deed773c29e51e56a4b9", string.Empty), // PS3 Firmware 4.21
                    ("10c273d5390aa318986e81c6f3746b27", string.Empty), // PS3 Firmware 4.20
                    ("7e6dbb1708ab1df66f0a0f0e2987e8f4", string.Empty), // PS3 Firmware 4.11
                    ("2c3671d071279b4f4b4d07e7166eaf38", string.Empty), // PS3 Firmware 4.10
                    ("a015671ae8622523956f1bfa0d1d54a0", string.Empty), // PS3 Firmware 4.01
                    ("52419374ba45a3d4a2b2dface2594140", string.Empty), // PS3 Firmware 4.00
                    ("d2bfa473a2ab1c0ff33cd7e452a11684", string.Empty), // PS3 Firmware 3.74
                    ("077a6a0a9abf3622373e3daa53f3ec70", string.Empty), // PS3 Firmware 3.73
                    ("c7d179d273699c2e5d53e401264828f3", string.Empty), // PS3 Firmware 3.72
                    ("746a6ffb564f0808af951e2fc30199c8", string.Empty), // PS3 Firmware 3.71
                    ("7ee6b91bbd07dde1e65a0681de66745b", string.Empty), // PS3 Firmware 3.70
                    ("f4cbe2651e9a0c6115028043bdc2c5dd", string.Empty), // PS3 Firmware 3.66
                    ("3001e6becbea7abf30fc35a7819c4478", string.Empty), // PS3 Firmware 3.65
                    ("f446810aabec0af1340c02d852e4118c", string.Empty), // PS3 Firmware 3.61
                    ("91ee193a2fa921a6fce780fc40236e3b", string.Empty), // PS3 Firmware 3.60
                    ("2a52196399a4b96ea568aafa65d1a27e", string.Empty), // PS3 Firmware 3.56 v2
                    ("6e070c96e0464e993aaf9deac3660863", string.Empty), // PS3 Firmware 3.56 v1
                    ("ca595ad9f3af8f1491d9c9b6921a8c61", string.Empty), // PS3 Firmware 3.55
                    ("0215e26d1dadeb950471a9c3397a140a", string.Empty), // PS3 Firmware 3.50
                    ("6ba866514589155ab094099a9f358ffd", string.Empty), // PS3 Firmware 3.42
                    ("e07d2b84c9e9691c261b73e5f1aada20", string.Empty), // PS3 Firmware 3.41 v2
                    ("00c835be718fc3d5f793e130a2b74217", string.Empty), // PS3 Firmware 3.41 v1
                    ("88b2f8d458119f666c97d893c17201cd", string.Empty), // PS3 Firmware 3.40
                    ("6bdf1b2409d705a0136c40746c62e85d", string.Empty), // PS3 Firmware 3.30
                    ("3a08ef6164a7770ae3e7d5b9f366437a", string.Empty), // PS3 Firmware 3.21
                    ("f4c191ffba72e008bc750b091219e1f6", string.Empty), // PS3 Firmware 3.16
                    ("54ee80e14e479f8351a988eb9a472072", string.Empty), // PS3 Firmware 3.15
                    ("70296b36d559e35752ae6efd04f702c0", string.Empty), // PS3 Firmware 3.10
                    ("6d956116eb5094564359339f60650f56", string.Empty), // PS3 Firmware 3.01
                    ("c049de7a6a4b03d53e7f9fde04e0fc08", string.Empty), // PS3 Firmware 3.00
                    ("d69362743142953be7cb44a5fd5cc888", string.Empty), // PS3 Firmware 2.80
                    ("d6856f234f3066b94dc88ab430a63bfb", string.Empty), // PS3 Firmware 2.76
                    ("3352adc32e39ec5f7b4cdeb8b861052a", string.Empty), // PS3 Firmware 2.70
                    ("043067d8624040f9f3f1a8dc2e662bce", string.Empty), // PS3 Firmware 2.60
                    ("5b0e9d62ff6e9edc796db9a2916824f0", string.Empty), // PS3 Firmware 2.53
                    ("e9cd7c268667ee695a708bf00c6853d8", string.Empty), // PS3 Firmware 2.52
                    ("823ab63c96d570510b254a80755df446", string.Empty), // PS3 Firmware 2.50
                    ("da62cbc7eafde9f6792b8a76a4aef004", string.Empty), // PS3 Firmware 2.43
                    ("60a6c36e2dd2000b3fda3205428665dc", string.Empty), // PS3 Firmware 2.42
                    ("67c660325b0b97acdeda6c0913dc1f74", string.Empty), // PS3 Firmware 2.41
                    ("96e9def601672c5abcbb4604bb2346f1", string.Empty), // PS3 Firmware 2.40
                    ("0274382cb3e21aa90e319eba358588c6", string.Empty), // PS3 Firmware 2.36
                    ("db48cbcde359982c065c2f5503204b67", string.Empty), // PS3 Firmware 2.35
                    ("dd2e675fce5d2eb28ca93bc32edb3de6", string.Empty), // PS3 Firmware 2.30
                    ("d33eb3ef5721bc940ca29b4c80b96bde", string.Empty), // PS3 Firmware 2.20
                    ("b859ca0f15fe9516d85fd0d89444ac5b", string.Empty), // PS3 Firmware 2.17
                    ("73fe70e0198b80f01525b6b72774af25", string.Empty), // PS3 Firmware 2.16
                    ("08393f1bd8e91589c95837142caf0a58", string.Empty), // PS3 Firmware 2.10
                    ("1d8e69249aa1a5593307cf7d8ca8a331", string.Empty), // PS3 Firmware 2.01
                    ("f9df10bb57cdeeef8395c39415f54965", string.Empty), // PS3 Firmware 2.00
                    ("f172f9bd37ca3e779dfaaf20ebfe632b", string.Empty), // PS3 Firmware 1.94
                    ("9c95692d4dd1c1e17c2b0ce5dcaebba1", string.Empty), // PS3 Firmware 1.93
                    ("0f99147438936578951dd9d3f514000a", string.Empty), // PS3 Firmware 1.92
                    ("bcefc8569ae4c2f4b7c09fd52f728c02", string.Empty), // PS3 Firmware 1.90
                    ("25de800491e8e88104d7e30b7524e9dc", string.Empty), // PS3 Firmware 1.82
                    ("b81fe78b163b62ce8983ad990306fe57", string.Empty), // PS3 Firmware 1.81
                    ("fd9887626f734b46b271aec58e827cb2", string.Empty), // PS3 Firmware 1.80
                    ("468238974943b45c09aeb632853401f2", string.Empty), // PS3 Firmware 1.70
                    ("3a5f49bcee3948301aa698ed57ecb10e", string.Empty), // PS3 Firmware 1.60
                    ("891bf61f56808119c4968ac3a9101540", string.Empty), // PS3 Firmware 1.54
                    ("afd01d9c72b3b69c03a0206c45d87dda", string.Empty), // PS3 Firmware 1.51
                    ("9d7f3830fb3082febf303bd0ce2f17c9", string.Empty), // PS3 Firmware 1.50
                    ("c4b08159eaa9fce111e076fb255aa106", string.Empty), // PS3 Firmware 1.32
                    ("602a1373cf1845d56348698d7037ab1b", string.Empty), // PS3 Firmware 1.31
                    ("d2c1aa63e55f5eb74e9846112d843f2b", string.Empty), // PS3 Firmware 1.30
                    ("8b6b4fb0a5fdd68bf85f02d4f6a0f096", string.Empty), // PS3 Firmware 1.11
                    ("374371771ad1608d0684c4d588440f0b", string.Empty), // PS3 Firmware 1.10
                    ("da5438f39b3b6e5ef904d91162d93086", string.Empty) // PS3 Firmware 1.02
                }
            },
            {
                "PSP2UPDAT.PUP",
                new[]
                {
                    ("f2c7b12fe85496ec88a0391b514d6e3b", string.Empty), // PS Vita Firmware 3.74
                    ("034ab948bbf1a002e0a058c602184b32", string.Empty), // PS Vita Firmware 3.73
                    ("84a422f3ae0671de01e0f56d8de88a36", string.Empty), // PS Vita Firmware 3.72
                    ("46b79772448291194608b44b17235da2", string.Empty), // PS Vita Firmware 3.71
                    ("54227fd645fcb6f177102cbd1f2d1924", string.Empty), // PS Vita Firmware 3.70
                    ("03448696e4e7f31e47c60f9b77fe632a", string.Empty), // PS Vita Firmware 3.69
                    ("fdc6f6bb6eed9fd82f7c4bc7414eaf4c", string.Empty), // PS Vita Firmware 3.68
                    ("0ace865229ec4c3f6fdf310ee083b11e", string.Empty), // PS Vita Firmware 3.67
                    ("0a0f2a9ae58968ac5d1d2127049c3cba", string.Empty), // PS Vita Firmware 3.65
                    ("43fa84109f0ef773d7c0553387b55b50", string.Empty), // PS Vita Firmware 3.63
                    ("99d9ab3e57a677b38e42cc6409b95e3b", string.Empty), // PS Vita Firmware 3.61
                    ("cd80b3da55771c99557db552542dff2b", string.Empty), // PS Vita Firmware 3.60
                    ("6c0e5dd66f5ba1e517b4311e471360d0", string.Empty), // PS Vita Firmware 3.57
                    ("5759c9df04450e1dd6a0c3d18c8a4e82", string.Empty), // PS Vita Firmware 3.55
                    ("3139fc516ee1b1c285a6dd7aa23cdfe2", string.Empty), // PS Vita Firmware 3.52
                    ("c74d838af1fba48b97aae4cd9a5bc395", string.Empty), // PS Vita Firmware 3.51
                    ("27140a86d3934ca83ad0b8401294083c", string.Empty), // PS Vita Firmware 3.50
                    ("c85fefb7957caaa1cba9e69474abea97", string.Empty), // PS Vita Firmware 3.36
                    ("0cb2594e90a2677e3ab1aef19b928617", string.Empty), // PS Vita Firmware 3.35
                    ("5b9886f1343880087101d11215d2c3c3", string.Empty), // PS Vita Firmware 3.30
                    ("977f3eb1e691ffc4d5e7b47a2a21beab", string.Empty), // PS Vita Firmware 3.18
                    ("1632804c073df1e73719fbecb790e03f", string.Empty), // PS Vita Firmware 3.15
                    ("369cce536878cf976c4b1e673418096f", string.Empty), // PS Vita Firmware 3.12
                    ("c0acc19376feb14d4dc100ca3a7f82d7", string.Empty), // PS Vita Firmware 3.10
                    ("329c1d98cbb2f6cc445ebfea9be9496c", string.Empty), // PS Vita Firmware 3.01
                    ("1d4cd717012dcff36667716c392729ce", string.Empty), // PS Vita Firmware 3.00
                    ("31d4ec0473c6e84ea63fd8c428a9855c", string.Empty), // PS Vita Firmware 2.61
                    ("749aae5fe7d6162f59a450939d859ee0", string.Empty), // PS Vita Firmware 2.60
                    ("6747cf9cafe4c8f804193c7c37349869", string.Empty), // PS Vita Firmware 2.12
                    ("1131cb560fe583e26be90770857b5c18", string.Empty), // PS Vita Firmware 2.11
                    ("03a8767820cd3d4384ae7636648e8740", string.Empty), // PS Vita Firmware 2.10
                    ("10f90fe07b9b416fd70dab006a97fd8f", string.Empty), // PS Vita Firmware 2.06
                    ("db28ae7d77dba9d2c31431aa08e991ae", string.Empty), // PS Vita Firmware 2.05
                    ("5ae40582f6bbd582bae91edcf2db982a", string.Empty), // PS Vita Firmware 2.02
                    ("0625bf047254ab121417febd1a7bfbd4", string.Empty), // PS Vita Firmware 2.01
                    ("fab2db211065450e8091ee506b067973", string.Empty), // PS Vita Firmware 2.00
                    ("3c564c2241988c52f99a31668d59faf0", string.Empty), // PS Vita Firmware 1.81
                    ("f76673af255d5a6e183eae5dd1ae05b5", string.Empty), // PS Vita Firmware 1.80
                    ("6075c28175518a84eb47de7e84b30eb2", string.Empty), // PS Vita Firmware 1.69.1
                    ("c3a1589e8d029a72879f35d218c8d618", string.Empty), // PS Vita Firmware 1.69.0
                    ("b872a4783a0de272a3bef6ba2b2b1ed9", string.Empty), // PS Vita Firmware 1.67
                    ("fb68d6099eab0ba188fd92ebff5085a2", string.Empty), // PS Vita Firmware 1.66
                    ("03a1888a00a58acd2316cbec309716a9", string.Empty), // PS Vita Firmware 1.65
                    ("f0bc997c94a6190768515c7d1f6db7b3", string.Empty), // PS Vita Firmware 1.61
                    ("5e9c9bc4f434db1401d30ec41c174ab9", string.Empty), // PS Vita Firmware 1.60
                    ("01a72de4dd90191f679f648da8d11a48", string.Empty), // PS Vita Firmware 1.52
                    ("48ac631ecae3837a7530506de0d73eaf", string.Empty), // PS Vita Firmware 1.51
                    ("f090a69bcf392b5c311b9e786c5cc0b5", string.Empty), // PS Vita Firmware 1.50
                    ("1ba3f7d0e2d0e0af797b9a169364b87f", string.Empty), // PS Vita Firmware 1.06
                    ("ed7cb2aacd6409db306daca78fc2345b", string.Empty), // PS Vita Firmware 1.05
                    ("9ca4a989a62899fd940c282b1e1df508", string.Empty) // PS Vita Firmware 1.04
                }
            },
            {
                "11.17.0-50U.zip",
                new[]
                {
                    ("e2f4d95f4fd8009d6785808f0e7a1749", string.Empty),
                    ("6e81f4fe7b892fc89c2f4be604731475", string.Empty)
                }
            },
            {
                "11.17.0-50E.zip",
                new[]
                {
                    ("d5357f47ead9fb98fb7d2522327693f8", string.Empty),
                    ("ebef8f0f53d82b5453225252a83f0f0b", string.Empty)
                }
            },
            {
                "11.17.0-50J.zip",
                new[]
                {
                    ("b2fb9eed4fb1fdf82c60ff507afa9fe2", string.Empty),
                    ("0e29160de755d0448276767db9e4d543", string.Empty)
                }
            },
            {
                "11.16.0-49U.zip",
                new[]
                {
                    ("cd8a07a2c93f1b4b87248e37d3ab9df6", string.Empty),
                    ("d1acefda82ebc4c4ef648938f92594d9", string.Empty)
                }
            },
            {
                "11.16.0-49E.zip",
                new[]
                {
                    ("ab42e913b49cb46220b886acc37cb44c", string.Empty),
                    ("315deb0c30f3c5061ed0bf9b38416f3e", string.Empty)
                }
            },
            {
                "11.16.0-49J.zip",
                new[]
                {
                    ("96ec482f63b4bfaffc61e6d3a6e1af43", string.Empty),
                    ("e5042b44582909971e6c6b6bfaf3c362", string.Empty)
                }
            },
            {
                "11.16.0-48U.zip",
                new[]
                {
                    ("e216e99a18ede8d6ad81ac022a0b6d4a", string.Empty),
                    ("5acba6ad943fde6ad559c7f21df54169", string.Empty)
                }
            },
            {
                "11.16.0-48E.zip",
                new[]
                {
                    ("b0fbd936ec66f5e01064aa040c741a19", string.Empty),
                    ("82ae8c5adb9f87f1a07350fd0be4b420", string.Empty)
                }
            },
            {
                "11.16.0-48J.zip",
                new[]
                {
                    ("8ceeac3be7e76f9f3db92628750d7bf9", string.Empty),
                    ("e2639a21cc3d3b7bbcabbf74b0dadc05", string.Empty)
                }
            },
            {
                "11.15.0-47U.zip",
                new[]
                {
                    ("267da56aac579e0255f0af3fb4b594ed", string.Empty),
                    ("32e70868dd60e78b4ba9a654bc9dea0d", string.Empty)
                }
            },
            {
                "11.15.0-47E.zip",
                new[]
                {
                    ("370dfc7e09631e17159f6f3da402f1c6", string.Empty),
                    ("961aae7dc978edea948b59f40b629f27", string.Empty)
                }
            },
            {
                "11.15.0-47J.zip",
                new[]
                {
                    ("2147994624b15a032428852bb44bd11e", string.Empty),
                    ("2f3d0f5baa7a6e02190b95ee153ea5d4", string.Empty)
                }
            },
            {
                "11.14.0-46U.zip",
                new[]
                {
                    ("38f236522d6b2d5aa7242161dbb89738", string.Empty),
                    ("e56f87b81f4b15fad17a15d0f0a26c02", string.Empty)
                }
            },
            {
                "11.14.0-46E.zip",
                new[]
                {
                    ("6330d16d9cf4bca3ec2befa701429fc5", string.Empty),
                    ("3f79511d5f116933ae8469ebe001ff7b", string.Empty)
                }
            },
            {
                "11.14.0-46J.zip",
                new[]
                {
                    ("cc8f934d5aeeb05023bc11e4a0616c24", string.Empty),
                    ("f961b972923636272c8a6632c6f17a9b", string.Empty)
                }
            },
            {
                "11.13.0-45U.zip",
                new[]
                {
                    ("c9ad138bcb1736a665c87a4fd9bf96f4", string.Empty),
                    ("6e6fb672b43eaac3fcf41a5ae6cc2206", string.Empty)
                }
            },
            {
                "11.13.0-45E.zip",
                new[]
                {
                    ("6ad64b2a72ebdbe82b11950f86cfa32b", string.Empty),
                    ("8ecd325cde30c14e3f3b5adb78b69e9e", string.Empty)
                }
            },
            {
                "11.13.0-45J.zip",
                new[]
                {
                    ("2ce2fd6d847269b0e5c25a6d84db7b4d", string.Empty),
                    ("3b4eb8e81d7aa2ce14de3b571abf63fb", string.Empty)
                }
            },
            {
                "11.12.0-44U.zip",
                new[]
                {
                    ("36129092439818abfc5badf68f602ea3", string.Empty),
                    ("1fe5d38d3c6de0dff7c658289c87ccb1", string.Empty)
                }
            },
            {
                "11.12.0-44E.zip",
                new[]
                {
                    ("f70e91d387c3ea13de21acae00171720", string.Empty),
                    ("82f4b3f5d4705307289ea1e812bc6160", string.Empty)
                }
            },
            {
                "11.12.0-44J.zip",
                new[]
                {
                    ("bc8b7ba56c5a31d835fa36087549ed67", string.Empty),
                    ("ae11e8232e639d411d83729b11a091d1", string.Empty)
                }
            },
            {
                "11.11.0-43E.zip",
                new[]
                {
                    ("d22baed690464c8dc30209d3d78bd85e", string.Empty),
                    ("3abc31a8a500416c9a641fbe8be060a6", string.Empty)
                }
            },
            {
                "11.10.0-43U.zip",
                new[]
                {
                    ("2541bc36811423210a1b94e0369942cc", string.Empty),
                    ("627ce2add1929f0f37c8548876c4a221", string.Empty)
                }
            },
            {
                "11.10.0-43E.zip",
                new[]
                {
                    ("b62198c8e4ef5823835abee0ae52e8d3", string.Empty),
                    ("1f5b393cd5580125979a737a9f53e188", string.Empty)
                }
            },
            {
                "11.10.0-43J.zip",
                new[]
                {
                    ("a0a287f45d749ed5df93bab0892c579b", string.Empty),
                    ("d60acace618b020d398c04b91049ba03", string.Empty)
                }
            },
            {
                "11.9.0-42U.zip",
                new[]
                {
                    ("bfed7c0d4e9b9dc40c2c2f7995ec7c54", string.Empty),
                    ("acad95441a72be396cc8b0464257ee07", string.Empty)
                }
            },
            {
                "11.9.0-42E.zip",
                new[]
                {
                    ("2f2ec01ac368625bdd4472839521dbdb", string.Empty),
                    ("38dfc6669176e8b9c6509be146d1ff5b", string.Empty)
                }
            },
            {
                "11.9.0-42J.zip",
                new[]
                {
                    ("d01d554401d6553743bd533ed6aaac58", string.Empty),
                    ("c09347b6615affb6ab20eace9e44816e", string.Empty)
                }
            },
            {
                "11.8.0-41U.zip",
                new[]
                {
                    ("bacc659a95e3663f6f90b5262c2df1cf", string.Empty),
                    ("03c129bba96d3f0d4634d5e8e6e22248", string.Empty)
                }
            },
            {
                "11.8.0-41E.zip",
                new[]
                {
                    ("2f8c83ebc4aa16b9f120e1e37b2ad255", string.Empty),
                    ("1c27f72bacc76493d770fdf658b5ee27", string.Empty)
                }
            },
            {
                "11.8.0-41J.zip",
                new[]
                {
                    ("942dd01890ac4d4feb0622028ae2484b", string.Empty),
                    ("19bdfea3125906e7cd8174c43de55f93", string.Empty)
                }
            },
            {
                "11.7.0-40U.zip",
                new[]
                {
                    ("7a5957c8e4a143e4fae216b63738e7b7", string.Empty),
                    ("5a4b4a1c9358654b43625ca33b4cb4f8", string.Empty)
                }
            },
            {
                "11.7.0-40E.zip",
                new[]
                {
                    ("3c6484ab6c336c8a028198c869a05b02", string.Empty),
                    ("c6a5433ae0b6f2d38e8de95d2e776148", string.Empty)
                }
            },
            {
                "11.7.0-40J.zip",
                new[]
                {
                    ("09ea56826917e56461ff332f2b032e89", string.Empty),
                    ("14e94a39fe6424a7d29e02ed48246ad4", string.Empty)
                }
            },
            {
                "11.6.0-39E.zip",
                new[]
                {
                    ("637fca21902821e290675eb904e0344e", string.Empty),
                    ("e06637ec5ec7a5229e98f15e982c6ac4", string.Empty)
                }
            },
            {
                "11.6.0-39J.zip",
                new[]
                {
                    ("efc4706d7d827d05fe86716d22d92968", string.Empty),
                    ("4019cd23f65464d2446f1e71372b6796", string.Empty)
                }
            },
            {
                "11.5.0-38U.zip",
                new[]
                {
                    ("6c9809ce3343d41819da1231bd4c45a0", string.Empty),
                    ("bdaf12f594ac868ec35ada7bc973c644", string.Empty)
                }
            },
            {
                "11.5.0-38E.zip",
                new[]
                {
                    ("f71c3737f33ab38ea69f9012ad0cc512", string.Empty),
                    ("f3e28ca3ba0fa282e828fa3abc3311c2", string.Empty)
                }
            },
            {
                "11.5.0-38J.zip",
                new[]
                {
                    ("093b2996ac2385146383fd02050c41aa", string.Empty),
                    ("2f4c268edf9686730a95bb04a02eae4d", string.Empty)
                }
            },
            {
                "11.4.0-37U.zip",
                new[]
                {
                    ("b6296e395db88fe1f9bc3a91647c8426", string.Empty),
                    ("dabe486394c989a5ca90d74c1b57a8ad", string.Empty)
                }
            },
            {
                "11.4.0-37E.zip",
                new[]
                {
                    ("f016b142034cf9ef671472eed53c5109", string.Empty),
                    ("4381e853c0905c49ea048a298bdf28b0", string.Empty)
                }
            },
            {
                "11.4.0-37J.zip",
                new[]
                {
                    ("be1e4a57b2bdb010dde0807902b831d9", string.Empty),
                    ("ffdb700a178a7a4c66db17af36ab5278", string.Empty)
                }
            },
            {
                "11.3.0-36U.zip",
                new[]
                {
                    ("a69eaa6ee9d9761fd59bfca7c0a6b799", string.Empty),
                    ("92e029d550c43ed27574178d077692e2", string.Empty)
                }
            },
            {
                "11.3.0-36E.zip",
                new[]
                {
                    ("64816a2a9b097d1286dbcde5d8f490d4", string.Empty),
                    ("ed20c378551d13a6541c35ba532f9ea2", string.Empty)
                }
            },
            {
                "11.3.0-36J.zip",
                new[]
                {
                    ("f8033489f92a25afa3e46ebc05487ab4", string.Empty),
                    ("361c39d78fc77f8f0398f12c9f67a378", string.Empty)
                }
            },
            {
                "11.2.0-35U.zip",
                new[]
                {
                    ("89ddfedcb953a35a11060548c9616470", string.Empty),
                    ("15de65b09383d69c9f76d7e20cbea3d9", string.Empty)
                }
            },
            {
                "11.2.0-35E.zip",
                new[]
                {
                    ("5a6764306b3cd2d1ae582d14cb4764ca", string.Empty),
                    ("154f4886c12e227b3219bde3468ed279", string.Empty)
                }
            },
            {
                "11.2.0-35J.zip",
                new[]
                {
                    ("d73260ee74971b6b30596331056eaf01", string.Empty),
                    ("4a1bd2179758ba3ee740969bce9456e4", string.Empty)
                }
            },
            {
                "11.1.0-34U.zip",
                new[]
                {
                    ("6ffb6be42a41d5f8369970b23bb44db9", string.Empty),
                    ("61cdbdaca8b80857dc988c9c3842a6e3", string.Empty)
                }
            },
            {
                "11.1.0-34E.zip",
                new[]
                {
                    ("03bd8f7eb58a1514040e0043bded0613", string.Empty),
                    ("5109757eb813e891112fae8766652537", string.Empty)
                }
            },
            {
                "11.1.0-34J.zip",
                new[]
                {
                    ("84b24584dce6a187a84d21135648dc7f", string.Empty),
                    ("7570c132aad121cdb6d5d4f63eba835c", string.Empty)
                }
            },
            {
                "11.0.0-33U.zip",
                new[]
                {
                    ("16cb87773aacefbf935e0853142cbe36", string.Empty),
                    ("9d608b9009a76d2f6b62f22bb91a6021", string.Empty)
                }
            },
            {
                "11.0.0-33E.zip",
                new[]
                {
                    ("8916280c32eb91f7b891537a7dd7d988", string.Empty),
                    ("a3c0a38f6c5079b267a834664818470a", string.Empty)
                }
            },
            {
                "11.0.0-33J.zip",
                new[]
                {
                    ("ac36a77e7ed3a620485300e611940ec2", string.Empty),
                    ("9d433cfeb888db436d405092d1c33e63", string.Empty)
                }
            },
            {
                "10.7.0-32E.zip",
                new[]
                {
                    ("bea4990b9158ee232464802f7344f040", string.Empty),
                    ("7d3301ea9599819b679c229b0257600a", string.Empty)
                }
            },
            {
                "10.3.0-28U.zip",
                new[]
                {
                    ("9084357c62a408846aa21c029938591b", string.Empty),
                    ("bbbf94658b9037973c25660f8e821fb1", string.Empty)
                }
            },
            {
                "10.3.0-28E.zip",
                new[]
                {
                    ("99a993714c2cbb73a9c54f7a4360d15d", string.Empty),
                    ("5968f4d489505249afeb0e7c8b3f4d12", string.Empty)
                }
            },
            {
                "10.3.0-28J.zip",
                new[]
                {
                    ("d82afe343b9c8e8bf1a9025615d68424", string.Empty),
                    ("4792090a8451251c4ae79cfac7bf23d9", string.Empty)
                }
            },
            {
                "10.0.0-27U.zip",
                new[]
                {
                    ("90e0a7431724789aae269ba4accabfc0", string.Empty),
                    ("fb4e5358e6d35cca7b77ac1d3ed31d67", string.Empty)
                }
            },
            {
                "9.6.0-24U.zip",
                new[]
                {
                    ("ea8dfb7864a803116eb5236d9377a535", string.Empty),
                    ("0282d343d0b97e3bcb15d07b16f6fbcd", string.Empty)
                }
            },
            {
                "9.6.0-24E.zip",
                new[]
                {
                    ("ba491753178d2f997df068f115dfab3f", string.Empty),
                    ("d2fa614cb647339f906f55a591bc0bad", string.Empty)
                }
            },
            {
                "9.6.0-24J.zip",
                new[]
                {
                    ("dc0ef64b9b90134f2c7af2a4213cad66", string.Empty),
                    ("80e94a2d66d7f024882adef2997a34a6", string.Empty)
                }
            },
            {
                "9.5.0-23U.zip",
                new[]
                {
                    ("f440c4e8f49227f5de71c85ba27db440", string.Empty),
                    ("12493deb271571cedc68478cc65b9610", string.Empty)
                }
            },
            {
                "9.5.0-23E.zip",
                new[]
                {
                    ("96a5daaede2dba0d58883998f0ddf5ec", string.Empty),
                    ("8b04faefe7e8f7409d50e2bbbe698d7e", string.Empty)
                }
            },
            {
                "9.5.0-23J.zip",
                new[]
                {
                    ("f5ba1b25a0c34266ebcbaef6c7adfb4c", string.Empty),
                    ("1895fed25d5c10eaab8d7541d079b625", string.Empty)
                }
            },
            {
                "9.4.0-21U.zip",
                new[]
                {
                    ("33740cb6924a9d45e4c2b31315fbbda6", string.Empty),
                    ("89f8389289f8e9f4aa1eaa7927224028", string.Empty)
                }
            },
            {
                "9.4.0-21E.zip",
                new[]
                {
                    ("79cbb7ce6570ecb5c882b9163b856e88", string.Empty),
                    ("093923a16e91b5330ab0bb795a5d1e1d", string.Empty)
                }
            },
            {
                "9.4.0-21J.zip",
                new[]
                {
                    ("030f0ba391ba9fa6d8f648ddbd1ce676", string.Empty),
                    ("c083908df6b6aa04081563c53f6d9674", string.Empty)
                }
            },
            {
                "9.2.0-20U.zip",
                new[]
                {
                    ("879f437d7c78f09ae565f79f5aa9c421", string.Empty),
                    ("e5f40c3b155cfd12ef38be35d2e81d2e", string.Empty)
                }
            },
            {
                "9.2.0-20E.zip",
                new[]
                {
                    ("caec3e015bc065dd0ca512f219c8fc3c", string.Empty),
                    ("6dcca8fcbc9a28c82c2a4e5182f3acf8", string.Empty)
                }
            },
            {
                "9.2.0-20J.zip",
                new[]
                {
                    ("a7c14f50e553d99d93efd3a8dd4f8b9d", string.Empty),
                    ("f46677696ed4bf0e182de5a8168b7542", string.Empty)
                }
            },
            {
                "9.1.0-20U.zip",
                new[]
                {
                    ("4c9b3a0d35de714f3185d5a5ee5b34a4", string.Empty),
                    ("27ed1a8bc79186c545e7ab90782123dc", string.Empty)
                }
            },
            {
                "9.0.0-20U.zip",
                new[]
                {
                    ("8a046251fe431f42c5dd5e33189b4ec8", string.Empty),
                    ("2cf7038ecfdf43f6fccdfe88e57c9d05", string.Empty)
                }
            },
            {
                "9.0.0-20J.zip",
                new[]
                {
                    ("5778e084cccb9de8725f31d2ce9e0344", string.Empty),
                    ("2b46b60834b2c825427ef321683ad2d8", string.Empty)
                }
            }
        };

        private static readonly Dictionary<string, string> PlatformDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ps1", "Sony PlayStation (PS1)" },
            { "ps2", "Sony PlayStation 2 (PS2)" },
            { "ps3", "Sony PlayStation 3 (PS3)" },
            { "psp", "Sony PlayStation Portable (PSP)" },
            { "psvita", "Sony PlayStation Vita (PS Vita)" },
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
            { "3ds", "Nintendo 3DS" },
            { "pokemonmini", "Nintendo Pokémon Mini" },
            { "gamecube", "Nintendo GameCube" },
            { "switch", "Nintendo Switch" },
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

        internal static readonly List<BiosFile> CanonicalBiosCatalog = new List<BiosFile>
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
                ExpectedSha1 = "f6bc2d1f5eb6593de7d089c425ac681d6fffd3f0",
                Description = "PlayStation BIOS (EU v3.0)",
                IsRequired = true
            },

            // Sony PlayStation 2 (PS2)
            new BiosFile
            {
                GamePlatform = "ps2",
                FileName = "scph39001.bin",
                ExpectedMd5 = "d5ce2c7d119f563ce04bc04dbc3a323e",
                ExpectedSha1 = "f9a5d629a036b99128f7cb530c6e3ca016e9c8b7",
                Description = "PlayStation 2 BIOS (US v1.60)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "ps2",
                FileName = "scph70012.bin",
                ExpectedMd5 = "d333558cc14561c1fdc334c75d5f37b7",
                ExpectedSha1 = "7a62e5f48603582707e9898eb055ea3eaee50d4c",
                Description = "PlayStation 2 Slim BIOS (US v2.00)",
                IsRequired = false
            },

            // Sony PSP
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "661.PBP",
                ExpectedMd5 = "6be8878f475ac5b1a499b95ab2f7d301",
                ExpectedSha1 = "bc10ad2114e1c349f23ee2b130d82f7e863460ac",
                Description = "PlayStation Portable Official Firmware 6.61",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/661.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "660.PBP",
                ExpectedMd5 = "2ca64d59dcf48f45fb99b400a586b395",
                ExpectedSha1 = "9384597afb7317d7b99376450e12fc6b31240f75",
                Description = "PlayStation Portable Official Firmware 6.60",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/660.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "650.zip",
                ExpectedMd5 = "48fdf936a5912963278d6e8049111ca5",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.50 (ZIP)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/650.zip"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "639.PBP",
                ExpectedMd5 = "ccce1a0f3ba08e22c26ec5bc047a0063",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.39",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/639.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "638.PBP",
                ExpectedMd5 = "d033298a1de455a5d90e06f91c6802f6",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.38",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/638.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "637.PBP",
                ExpectedMd5 = "5a21c511c90ed765747c43a9779f7a4b",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.37",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/637.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "636.PBP",
                ExpectedMd5 = "bcac2b0a4ac55378f300386391e4bb37",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.36",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/636.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "635.PBP",
                ExpectedMd5 = "5945c21abfa5b1221adda745f9417668",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.35",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/635.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "631.PBP",
                ExpectedMd5 = "d7ea54162d2d53c5efb46648ee12f6bc",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.31",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/631.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "630.PBP",
                ExpectedMd5 = "42601ed4266d175b37ff0d3e6d079f3b",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.30",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/630.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "620.PBP",
                ExpectedMd5 = "16d8cacc0cdb00a2a6a8c1b77d7dd136",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.20",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/620.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "610.PBP",
                ExpectedMd5 = "610d3d9ed39befa9c10ce139df01ebfb",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.10",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/610.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "600.PBP",
                ExpectedMd5 = "a734c9829b20d17d7fc7a149d8274a8a",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 6.00",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/600.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "570.PSAR",
                ExpectedMd5 = "42601831bf10e9bbf296c3ab3e782384",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.70 (Only PSAR)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/570.PSAR"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "555.PBP",
                ExpectedMd5 = "d7574bf9504c957e92d98470f1699a27",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.55",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/555.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "551.PBP",
                ExpectedMd5 = "148d2a024741df54eacabbc79d9f3014",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.51",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/551.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "550.PBP",
                ExpectedMd5 = "eacd91e92abf817ff4ceaf02258f73a6",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.50",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/550.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "505.PBP",
                ExpectedMd5 = "2b0581f4a8752e4171cbdb7a406a2ec0",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.05",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/505.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "503.PBP",
                ExpectedMd5 = "565c702771472445342a3b8c56578961",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.03",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/503.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "502.PBP",
                ExpectedMd5 = "41b80591c6024a278945f1cdd72aa892",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.02",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/502.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "501.PBP",
                ExpectedMd5 = "46e66cef9ec8a63eb5af2df0a53e6b57",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.01",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/501.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "500.PBP",
                ExpectedMd5 = "60aa0356d4c66f589b71cfc9abc08713",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 5.00",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/500.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "421.ZIP",
                ExpectedMd5 = "a842978fa35500073c20bf02777997a5",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.21 (ZIP)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/421.ZIP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "421.PSAR",
                ExpectedMd5 = "edea8c2151a81ebb3c6f145f9034f5a1",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.21 (Only PSAR)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/421.PSAR"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "420.ZIP",
                ExpectedMd5 = "18bf0cd6386c44322c8d0db561eb0b30",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.20 (ZIP)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/420.ZIP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "405.PBP",
                ExpectedMd5 = "8497dd2b3485e934b073948b558221c3",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.05",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/405.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "401.PBP",
                ExpectedMd5 = "c593abc023aa9b215710df6e263b3fff",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.01",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/401.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "400.PBP",
                ExpectedMd5 = "bf8d5081adc08457debe062270646c45",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 4.00",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/400.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "396.PBP",
                ExpectedMd5 = "6046ec031a2757a59fbc2065484b1fe1",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.96",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/396.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "395.PBP",
                ExpectedMd5 = "32720ecf54e656c401ffc92d33fe62d9",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.95",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/395.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "393.PBP",
                ExpectedMd5 = "af24e8ca2f6b54fd98409dbc2a627157",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.93",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/393.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "390.PBP",
                ExpectedMd5 = "64bae5dd0d0f836baedf915fb106e9cc",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.90",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/390.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "380.PBP",
                ExpectedMd5 = "923297bb6c6aead603d745e9e724fea0",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.80",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/380.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "373.PBP",
                ExpectedMd5 = "0c1668bb95c6e92091c743a53b809997",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.73",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/373.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "372.PBP",
                ExpectedMd5 = "05d9fe3c72a263d8a4490a8ec727aad3",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.72",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/372.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "371.PBP",
                ExpectedMd5 = "6babee6e1415ae27999928566adbd569",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.71",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/371.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "370.PBP",
                ExpectedMd5 = "6942dccc46cb92161ed1568a784b2ded",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.70",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/370.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "360.ZIP",
                ExpectedMd5 = "3966394f594affc7e4b5f5d3030e708c",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.60 (ZIP)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/360.ZIP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "360.PSAR",
                ExpectedMd5 = "eaa13343c70d9b0337c75b295474c69a",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.60 (Only PSAR)",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/360.PSAR"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "352.PBP",
                ExpectedMd5 = "9e8da3f26e06b6b04fd31f3fda6069a5",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.52",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/352.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "351.PBP",
                ExpectedMd5 = "9593f0783ebb97f0ea0c0ed0ee66dec5",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.51",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/351.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "350.PBP",
                ExpectedMd5 = "b4e70aea4b09e74aacbe2d83b8868f2c",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.50",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/350.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "340.PBP",
                ExpectedMd5 = "58101e3f6e897725ac55ec1ae3dc07a0",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.40",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/340.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "330.PBP",
                ExpectedMd5 = "842515d85bc1645e3ba2e98533968c62",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.30",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/330.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "311.PBP",
                ExpectedMd5 = "ccd985a6145ecd572aa4a94d483404fa",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.11",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/311.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "310.PBP",
                ExpectedMd5 = "81b8ca8f63db44996f6f5fbf03c5dee1",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.10",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/310.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "303.PBP",
                ExpectedMd5 = "a1dd70c8e8f12d83d954951d9e0d6838",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.03",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/303.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "302.PBP",
                ExpectedMd5 = "6a1530e6a1c7ec7bf9f50464d4110e0c",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.02",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/302.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "301.PBP",
                ExpectedMd5 = "1dc76eab462c2f9c71aa8536659682f8",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.01",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/301.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "300.PBP",
                ExpectedMd5 = "fd90b0c3ffa0fc73cfecae4945ac3c47",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 3.00",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/300.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "282.PBP",
                ExpectedMd5 = "65e58702b08898da4f155c8d217e5f41",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.82",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/282.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "281.PBP",
                ExpectedMd5 = "4726589a847329cb03ac5ffd5096d62e",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.81",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/281.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "280.PBP",
                ExpectedMd5 = "8a0d85b6e2d259a3b5710378d892a1ef",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.80",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/280.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "271.PBP",
                ExpectedMd5 = "d6f3d5dc926c00a8a633361005018813",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.71",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/271.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "270.PBP",
                ExpectedMd5 = "aea0541d63cb1d5112284efb1fb833a0",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.70",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/270.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "260.PBP",
                ExpectedMd5 = "a69a022fce43b614a8cb305786f59855",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.60",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/260.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "250.PBP",
                ExpectedMd5 = "00df561c8215e42b16661ecde4c57e14",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.50",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/250.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "201.PBP",
                ExpectedMd5 = "c9629365e58daf4973019c78727f521c",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.01",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/201.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "200v2.PBP",
                ExpectedMd5 = "50c7032754835b588319c1a6c652cdc0",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.00 v2",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/200v2.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "200v1.PBP",
                ExpectedMd5 = "62f6d3d18038f463092e3d6653fae195",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 2.00 v1",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/200v1.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "152.PBP",
                ExpectedMd5 = "b976783070c12c4ed81cc27785222491",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 1.52",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/152.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "151.PBP",
                ExpectedMd5 = "ad79cbaa4838d9ed2a16f403567a5222",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 1.51",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/151.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "150.PBP",
                ExpectedMd5 = "e32eb27980ba8b291eb803e3397e4b24",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 1.50",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/150.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "100.PBP",
                ExpectedMd5 = "47398e49360ab021c3ffa42cac3e6f15",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation Portable Official Firmware 1.00",
                IsRequired = false,
                DownloadUrl = "https://archive.org/download/psp_ofw_firmwares/PSP/100.PBP"
            },
            new BiosFile
            {
                GamePlatform = "psp",
                FileName = "ppge_atlas.zim",
                ExpectedMd5 = "866855cc330b9b95cc69135fb7b41d38",
                ExpectedSha1 = "30a33f3651f8e1b0967a5dfe6ff714baebcce41a",
                Description = "PPSSPP Font & UI Atlas Firmware",
                IsRequired = false
            },

            // Sony PlayStation 3 (PS3)
            new BiosFile
            {
                GamePlatform = "ps3",
                FileName = "PS3UPDAT.PUP",
                ExpectedMd5 = "a2b60b6ac1d2e49e230144345616927c",
                ExpectedSha1 = string.Empty,
                Description = "PlayStation 3 Official Firmware 4.93 (also accepts 1.02–4.92)",
                IsRequired = true
            },

            // Sony PlayStation Vita (PS Vita)
            new BiosFile
            {
                GamePlatform = "psvita",
                FileName = "PSP2UPDAT.PUP",
                ExpectedMd5 = "f2c7b12fe85496ec88a0391b514d6e3b",
                ExpectedSha1 = "cc72dfcc964577cc29112ef368c28f55277c237c",
                Description = "PlayStation Vita Official Firmware 3.74 (also accepts 1.04–3.73)",
                IsRequired = true,
                DownloadUrl = "https://archive.org/download/PSVita-Official-Firmwares/Normal/Firmware%203.74/PSP2UPDAT.PUP"
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
                ExpectedSha1 = "4846f448160059a7da0215a5df12ca160f26dd69",
                Description = "Mega-CD BIOS (JP v1.00)",
                IsRequired = true
            },

            // Sega Saturn
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "sega_101.bin",
                ExpectedMd5 = "85ec9ca47d8f6807718151cbcca8b964",
                ExpectedSha1 = "df94c5b4d47eb3cc404d88b33a8fda237eaf4720",
                Description = "Sega Saturn BIOS (JP v1.01)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "mpr-17933.bin",
                ExpectedMd5 = "3240872c70984b6cbfda1586cab68dbe",
                ExpectedSha1 = "faa8ea183a6d7bbe5d4e03bb1332519800d3fbc3",
                Description = "Sega Saturn BIOS (US/EU v1.00a)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "saturn",
                FileName = "mpr-18811-mx.ic1",
                ExpectedMd5 = "255113ba943c92a54facd25a10fd780c",
                ExpectedSha1 = "a67cd4f550751f8b91de2b8b74528ab4e0c11c77",
                Description = "The King of Fighters '95 ROM Cartridge BIOS",
                IsRequired = false
            },

            // Sega Dreamcast
            new BiosFile
            {
                GamePlatform = "dreamcast",
                FileName = "dc_boot.bin",
                ExpectedMd5 = "e10c53c2f8b90bab96ead2d368858623",
                ExpectedSha1 = "8951d1bb219ab2ff8583033d2119c899cc81f18c",
                Description = "Dreamcast Boot ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dreamcast",
                FileName = "dc_flash.bin",
                ExpectedMd5 = "0a93f7940c455905bea6e392dfde92a4",
                ExpectedSha1 = "94d44d7f9529ec1642ba3771ed3c5f756d5bc872",
                Description = "Dreamcast Flash ROM",
                IsRequired = true
            },

            // Sega NAOMI / Atomiswave
            new BiosFile
            {
                GamePlatform = "naomi",
                FileName = "naomi.zip",
                ExpectedMd5 = "eb4099aeb42ef089cfe94f8fe95e51f6",
                ExpectedSha1 = "d7ef86bd03de7c1d0e2b0762e04b6f8f8d26dbdb",
                Description = "Sega NAOMI Arcade System BIOS Pack",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "naomi",
                FileName = "awbios.zip",
                ExpectedMd5 = "85254fbe320ca82a768ec2c26bb08def",
                ExpectedSha1 = "7940c7bf29eee85a5b2fdec78750b19aa22895dc",
                Description = "Sammy Atomiswave Arcade BIOS",
                IsRequired = false
            },

            // Sega Master System & Game Gear
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_U.sms",
                ExpectedMd5 = "840481177270d5642a14ca71ee72844c",
                ExpectedSha1 = "c315672807d8ddb8d91443729405c766dd95cae7",
                Description = "Sega Master System BIOS (US v1.3)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_E.sms",
                ExpectedMd5 = "840481177270d5642a14ca71ee72844c",
                ExpectedSha1 = "c315672807d8ddb8d91443729405c766dd95cae7",
                Description = "Sega Master System BIOS (EU v1.3)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sms",
                FileName = "bios_J.sms",
                ExpectedMd5 = "24a519c53f67b00640d0048ef7089105",
                ExpectedSha1 = "a8c1b39a2e41137835eda6a5de6d46dd9fadbaf2",
                Description = "Sega Mark III / Master System BIOS (JP v2.1)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "gamegear",
                FileName = "bios.gg",
                ExpectedMd5 = "672e104c3be3a238301aceffc3b23fd6",
                ExpectedSha1 = "914aa165e3d879f060be77870d345b60cfeb4ede",
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
                ExpectedMd5 = "fed4d8242cfbed61343d53d48432aced",
                ExpectedSha1 = "604556b2e62860af18db5a77f2956ebc75450020",
                Description = "Satellaview BS-X Town Cartridge BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "64dd",
                FileName = "64DD_IPL.bin",
                ExpectedMd5 = "8d3d9f294b6e174bc7b1d2fd1c727530",
                ExpectedSha1 = "bf861922dcb78c316360e3e742f4f70ff63c9bc3",
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
                ExpectedSha1 = "aa2f50a77dfb4823da96ba99309085a3c6278515",
                Description = "Super Game Boy Boot ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sgb",
                FileName = "SGB1.sfc",
                ExpectedMd5 = "b15ddb15721c657d82c5bab6db982ee9",
                ExpectedSha1 = "973e10840db683cf3faf61bd443090786b3a9f04",
                Description = "Super Game Boy SNES Program ROM (SGB1)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "sgb",
                FileName = "SGB2.sfc",
                ExpectedMd5 = "8ecd73eb4edf7ed7e81aef1be80031d5",
                ExpectedSha1 = "e5b2922ca137051059e4269b236d07a22c07bc84",
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
                ExpectedMd5 = "e45033d9b0fa6b0de071292bba7c9d13",
                ExpectedSha1 = "cfe072921ee3fb93f688743f8beef89043c3e9ad",
                Description = "Nintendo DS System Firmware",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dsi",
                FileName = "bios7i.bin",
                ExpectedMd5 = "559dae4ea78eb9d67702c56c1d791e81",
                ExpectedSha1 = "a3aa751eb6bdaaf8a827ba9e03576a6f1ab0f547",
                Description = "Nintendo DSi ARM7 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "dsi",
                FileName = "bios9i.bin",
                ExpectedMd5 = "87b665fce118f76251271c3732532777",
                ExpectedSha1 = "7bf549b8be9e48ab0cdc9b0fdadd49a5131f97eb",
                Description = "Nintendo DSi ARM9 BIOS",
                IsRequired = true
            },

            // Nintendo 3DS
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.17.0-50U.zip",
                ExpectedMd5 = "e2f4d95f4fd8009d6785808f0e7a1749",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.17.0-50U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.17.0-50E.zip",
                ExpectedMd5 = "d5357f47ead9fb98fb7d2522327693f8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.17.0-50E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.17.0-50J.zip",
                ExpectedMd5 = "b2fb9eed4fb1fdf82c60ff507afa9fe2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.17.0-50J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-49U.zip",
                ExpectedMd5 = "cd8a07a2c93f1b4b87248e37d3ab9df6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-49U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-49E.zip",
                ExpectedMd5 = "ab42e913b49cb46220b886acc37cb44c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-49E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-49J.zip",
                ExpectedMd5 = "96ec482f63b4bfaffc61e6d3a6e1af43",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-49J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-48U.zip",
                ExpectedMd5 = "e216e99a18ede8d6ad81ac022a0b6d4a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-48U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-48E.zip",
                ExpectedMd5 = "b0fbd936ec66f5e01064aa040c741a19",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-48E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.16.0-48J.zip",
                ExpectedMd5 = "8ceeac3be7e76f9f3db92628750d7bf9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.16.0-48J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.15.0-47U.zip",
                ExpectedMd5 = "267da56aac579e0255f0af3fb4b594ed",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.15.0-47U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.15.0-47E.zip",
                ExpectedMd5 = "370dfc7e09631e17159f6f3da402f1c6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.15.0-47E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.15.0-47J.zip",
                ExpectedMd5 = "2147994624b15a032428852bb44bd11e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.15.0-47J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.14.0-46U.zip",
                ExpectedMd5 = "38f236522d6b2d5aa7242161dbb89738",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.14.0-46U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.14.0-46E.zip",
                ExpectedMd5 = "6330d16d9cf4bca3ec2befa701429fc5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.14.0-46E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.14.0-46J.zip",
                ExpectedMd5 = "cc8f934d5aeeb05023bc11e4a0616c24",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.14.0-46J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.13.0-45U.zip",
                ExpectedMd5 = "c9ad138bcb1736a665c87a4fd9bf96f4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.13.0-45U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.13.0-45E.zip",
                ExpectedMd5 = "6ad64b2a72ebdbe82b11950f86cfa32b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.13.0-45E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.13.0-45J.zip",
                ExpectedMd5 = "2ce2fd6d847269b0e5c25a6d84db7b4d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.13.0-45J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.12.0-44U.zip",
                ExpectedMd5 = "36129092439818abfc5badf68f602ea3",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.12.0-44U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.12.0-44E.zip",
                ExpectedMd5 = "f70e91d387c3ea13de21acae00171720",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.12.0-44E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.12.0-44J.zip",
                ExpectedMd5 = "bc8b7ba56c5a31d835fa36087549ed67",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.12.0-44J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.11.0-43E.zip",
                ExpectedMd5 = "d22baed690464c8dc30209d3d78bd85e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.11.0-43E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.10.0-43U.zip",
                ExpectedMd5 = "2541bc36811423210a1b94e0369942cc",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.10.0-43U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.10.0-43E.zip",
                ExpectedMd5 = "b62198c8e4ef5823835abee0ae52e8d3",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.10.0-43E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.10.0-43J.zip",
                ExpectedMd5 = "a0a287f45d749ed5df93bab0892c579b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.10.0-43J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.9.0-42U.zip",
                ExpectedMd5 = "bfed7c0d4e9b9dc40c2c2f7995ec7c54",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.9.0-42U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.9.0-42E.zip",
                ExpectedMd5 = "2f2ec01ac368625bdd4472839521dbdb",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.9.0-42E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.9.0-42J.zip",
                ExpectedMd5 = "d01d554401d6553743bd533ed6aaac58",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.9.0-42J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.8.0-41U.zip",
                ExpectedMd5 = "bacc659a95e3663f6f90b5262c2df1cf",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.8.0-41U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.8.0-41E.zip",
                ExpectedMd5 = "2f8c83ebc4aa16b9f120e1e37b2ad255",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.8.0-41E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.8.0-41J.zip",
                ExpectedMd5 = "942dd01890ac4d4feb0622028ae2484b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.8.0-41J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.7.0-40U.zip",
                ExpectedMd5 = "7a5957c8e4a143e4fae216b63738e7b7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.7.0-40U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.7.0-40E.zip",
                ExpectedMd5 = "3c6484ab6c336c8a028198c869a05b02",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.7.0-40E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.7.0-40J.zip",
                ExpectedMd5 = "09ea56826917e56461ff332f2b032e89",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.7.0-40J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.6.0-39U.zip",
                ExpectedMd5 = "b5cef34f20ce3f709c5d0bc380a7c9a7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.6.0-39U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.6.0-39E.zip",
                ExpectedMd5 = "637fca21902821e290675eb904e0344e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.6.0-39E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.6.0-39J.zip",
                ExpectedMd5 = "efc4706d7d827d05fe86716d22d92968",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.6.0-39J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.5.0-38U.zip",
                ExpectedMd5 = "6c9809ce3343d41819da1231bd4c45a0",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.5.0-38U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.5.0-38E.zip",
                ExpectedMd5 = "f71c3737f33ab38ea69f9012ad0cc512",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.5.0-38E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.5.0-38J.zip",
                ExpectedMd5 = "093b2996ac2385146383fd02050c41aa",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.5.0-38J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.4.0-37U.zip",
                ExpectedMd5 = "b6296e395db88fe1f9bc3a91647c8426",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.4.0-37U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.4.0-37E.zip",
                ExpectedMd5 = "f016b142034cf9ef671472eed53c5109",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.4.0-37E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.4.0-37J.zip",
                ExpectedMd5 = "be1e4a57b2bdb010dde0807902b831d9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.4.0-37J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.3.0-36U.zip",
                ExpectedMd5 = "a69eaa6ee9d9761fd59bfca7c0a6b799",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.3.0-36U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.3.0-36E.zip",
                ExpectedMd5 = "64816a2a9b097d1286dbcde5d8f490d4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.3.0-36E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.3.0-36J.zip",
                ExpectedMd5 = "f8033489f92a25afa3e46ebc05487ab4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.3.0-36J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.2.0-35U.zip",
                ExpectedMd5 = "89ddfedcb953a35a11060548c9616470",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.2.0-35U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.2.0-35E.zip",
                ExpectedMd5 = "5a6764306b3cd2d1ae582d14cb4764ca",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.2.0-35E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.2.0-35J.zip",
                ExpectedMd5 = "d73260ee74971b6b30596331056eaf01",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.2.0-35J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.1.0-34U.zip",
                ExpectedMd5 = "6ffb6be42a41d5f8369970b23bb44db9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.1.0-34U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.1.0-34E.zip",
                ExpectedMd5 = "03bd8f7eb58a1514040e0043bded0613",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.1.0-34E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.1.0-34J.zip",
                ExpectedMd5 = "84b24584dce6a187a84d21135648dc7f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.1.0-34J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.0.0-33U.zip",
                ExpectedMd5 = "16cb87773aacefbf935e0853142cbe36",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.0.0-33U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.0.0-33E.zip",
                ExpectedMd5 = "8916280c32eb91f7b891537a7dd7d988",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.0.0-33E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "11.0.0-33J.zip",
                ExpectedMd5 = "ac36a77e7ed3a620485300e611940ec2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 11.0.0-33J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.7.0-32U.zip",
                ExpectedMd5 = "0a4541d6059f1e2b3f000e8c9e2c2343",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.7.0-32U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.7.0-32E.zip",
                ExpectedMd5 = "bea4990b9158ee232464802f7344f040",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.7.0-32E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.7.0-32J.zip",
                ExpectedMd5 = "f41aae12030e3e7e23846a08d25eb8c4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.7.0-32J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.6.0-31U.zip",
                ExpectedMd5 = "84fa7f2782e491ed8197f2203afd625a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.6.0-31U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.6.0-31E.zip",
                ExpectedMd5 = "8b000c3f8512b92a23adcadb385b38fc",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.6.0-31E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.5.0-30U.zip",
                ExpectedMd5 = "643748520650494a43bf238fb97c1f63",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.5.0-30U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.5.0-30E.zip",
                ExpectedMd5 = "9229723d86beaa724224dbf1db2c5188",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.5.0-30E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.4.0-29U.zip",
                ExpectedMd5 = "d984b1c989be76c71ca625e8d5de5346",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.4.0-29U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.4.0-29E.zip",
                ExpectedMd5 = "3f5756311be03cfb34166df676e74339",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.4.0-29E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.3.0-28U.zip",
                ExpectedMd5 = "9084357c62a408846aa21c029938591b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.3.0-28U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.3.0-28E.zip",
                ExpectedMd5 = "99a993714c2cbb73a9c54f7a4360d15d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.3.0-28E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.3.0-28J.zip",
                ExpectedMd5 = "d82afe343b9c8e8bf1a9025615d68424",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.3.0-28J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.2.0-28U.zip",
                ExpectedMd5 = "f9fc9a7175ba633dca02175761a7537c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.2.0-28U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.2.0-28E.zip",
                ExpectedMd5 = "cff140c0da8f779888e33b9fbc42daae",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.2.0-28E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.1.0-27U.zip",
                ExpectedMd5 = "8688ea47c22beae3cc69dbb816a084ee",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.1.0-27U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.1.0-27E.zip",
                ExpectedMd5 = "b7209680f69040772dbd7642027e5cde",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.1.0-27E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.0.0-27U.zip",
                ExpectedMd5 = "90e0a7431724789aae269ba4accabfc0",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.0.0-27U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "10.0.0-27E.zip",
                ExpectedMd5 = "7e072dfa156d857006ffe68e5ebd7277",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 10.0.0-27E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.9.0-26U.zip",
                ExpectedMd5 = "546874d7848dc8122396c3f790e586f1",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.9.0-26U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.9.0-26E.zip",
                ExpectedMd5 = "150e4c66e170883557cd1d7b19f07a45",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.9.0-26E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.9.0-26J.zip",
                ExpectedMd5 = "658b76dad236b3790e6a84df100dc224",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.9.0-26J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.8.0-25U.zip",
                ExpectedMd5 = "6b3c35c54821b1d16d80df9622018e8a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.8.0-25U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.8.0-25E.zip",
                ExpectedMd5 = "d87140acfc6206c5ff19c02517a027d0",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.8.0-25E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.7.0-25U.zip",
                ExpectedMd5 = "1ce4d83185108b0fb222d584da0f3f0b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.7.0-25U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.7.0-25E.zip",
                ExpectedMd5 = "3668e9ec37a385052f1f4ae01577cee6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.7.0-25E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.6.0-24U.zip",
                ExpectedMd5 = "ea8dfb7864a803116eb5236d9377a535",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.6.0-24U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.6.0-24E.zip",
                ExpectedMd5 = "ba491753178d2f997df068f115dfab3f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.6.0-24E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.6.0-24J.zip",
                ExpectedMd5 = "dc0ef64b9b90134f2c7af2a4213cad66",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.6.0-24J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.5.0-23U.zip",
                ExpectedMd5 = "f440c4e8f49227f5de71c85ba27db440",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.5.0-23U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.5.0-23E.zip",
                ExpectedMd5 = "96a5daaede2dba0d58883998f0ddf5ec",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.5.0-23E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.5.0-23J.zip",
                ExpectedMd5 = "f5ba1b25a0c34266ebcbaef6c7adfb4c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.5.0-23J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.5.0-22E.zip",
                ExpectedMd5 = "79cbb7ce6570ecb5c882b9163b856e88",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.5.0-22E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.4.0-21U.zip",
                ExpectedMd5 = "33740cb6924a9d45e4c2b31315fbbda6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.4.0-21U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.4.0-21E.zip",
                ExpectedMd5 = "79cbb7ce6570ecb5c882b9163b856e88",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.4.0-21E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.4.0-21J.zip",
                ExpectedMd5 = "030f0ba391ba9fa6d8f648ddbd1ce676",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.4.0-21J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.3.0-21U.zip",
                ExpectedMd5 = "6b2368071e58e7cc2d3849167b937c82",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.3.0-21U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.3.0-21E.zip",
                ExpectedMd5 = "e4a7c2aabd070c25bf135409ad2d402f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.3.0-21E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20U.zip",
                ExpectedMd5 = "879f437d7c78f09ae565f79f5aa9c421",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20E.zip",
                ExpectedMd5 = "caec3e015bc065dd0ca512f219c8fc3c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20J.zip",
                ExpectedMd5 = "a7c14f50e553d99d93efd3a8dd4f8b9d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.1.0-20U.zip",
                ExpectedMd5 = "4c9b3a0d35de714f3185d5a5ee5b34a4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.1.0-20U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.0.0-20U.zip",
                ExpectedMd5 = "8a046251fe431f42c5dd5e33189b4ec8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.0.0-20U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.0.0-20E.zip",
                ExpectedMd5 = "fae41691da2ba1f33efe64aee2052b66",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.0.0-20E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.0.0-20J.zip",
                ExpectedMd5 = "5778e084cccb9de8725f31d2ce9e0344",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.0.0-20J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.1.0-19U.zip",
                ExpectedMd5 = "6551ba7708cc094e441198d174ffc37c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.1.0-19U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.1.0-19E.zip",
                ExpectedMd5 = "9ed9db696bb94341ad0ddc9144aa0966",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.1.0-19E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.1.0-18U.zip",
                ExpectedMd5 = "2319a3ff6ab1790be1d3078dfeac5c14",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.1.0-18U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.1.0-18E.zip",
                ExpectedMd5 = "9281fbd43c045cba7271bd6b67c8222c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.1.0-18E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.0.0-18U.zip",
                ExpectedMd5 = "517de0cb7c6dccdec15ea49eed75364a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.0.0-18U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "8.0.0-18E.zip",
                ExpectedMd5 = "36eddb7cc9c724af539588c073a549c6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 8.0.0-18E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.2.0-17U.zip",
                ExpectedMd5 = "58383e3c23b4c7a301ecb47fcd89329f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.2.0-17U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.2.0-17E.zip",
                ExpectedMd5 = "abbc8982ca632070f671943e9731a335",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.2.0-17E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-16U.zip",
                ExpectedMd5 = "182babe1ac56aca23ed6fd401975d343",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-16U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-16E.zip",
                ExpectedMd5 = "27ca6c8dbee3ef048c64378c3756d071",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-16E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-15U.zip",
                ExpectedMd5 = "3b5a6d26a7aecce3e31ad49fa3459aaf",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-15U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-15E.zip",
                ExpectedMd5 = "cd561e6ba39c88bcb17277c0cb908482",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-15E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-14U.zip",
                ExpectedMd5 = "1b755e7008a4515d7a048d969614b6e8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-14U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.1.0-14E.zip",
                ExpectedMd5 = "885af9d91a0c95008f6688d096751bc4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.1.0-14E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.0.0-13U.zip",
                ExpectedMd5 = "55b9dded395cf78bd846f5edc8649225",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.0.0-13U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "7.0.0-13E.zip",
                ExpectedMd5 = "fc17ad8b277e905499919bb855f243fd",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 7.0.0-13E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.4.0-12U.zip",
                ExpectedMd5 = "4eaac934067c7afe787d1f08781cf546",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.4.0-12U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.4.0-12E.zip",
                ExpectedMd5 = "a8324a669c88d9f51718b9a900146a19",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.4.0-12E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.3.0-12U.zip",
                ExpectedMd5 = "60d3abe7e45c673ad5370523fc7ef6e5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.3.0-12U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.3.0-12E.zip",
                ExpectedMd5 = "5c7c71a1e086d89211f54953c422eade",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.3.0-12E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.2.0-12U.zip",
                ExpectedMd5 = "f8f2db916e0e187ddd3b206573ba83e3",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.2.0-12U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.2.0-12E.zip",
                ExpectedMd5 = "ec1819520e5c96027b69474e0621ae9e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.2.0-12E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.1.0-12U.zip",
                ExpectedMd5 = "84f5234b5f2af6b8c6cddc467f5de730",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.1.0-12U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.1.0-11U.zip",
                ExpectedMd5 = "a7c34297548694f497c777d2210ff4e4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.1.0-11U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.1.0-11E.zip",
                ExpectedMd5 = "351e910dc1001c4a07b747e661af5a31",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.1.0-11E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "6.0.0-11U.zip",
                ExpectedMd5 = "423f2c64956af5132d8fa981f34fd60b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 6.0.0-11U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "5.1.0-11E.zip",
                ExpectedMd5 = "2ba364b6915eabf0a49663701ee77969",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 5.1.0-11E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "5.0.0-11U.zip",
                ExpectedMd5 = "2ce1769cd760a3777c69a258be8a610c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 5.0.0-11U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "5.0.0-11E.zip",
                ExpectedMd5 = "f1d4f0662345115d348fc2115b9de33b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 5.0.0-11E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "5.0.0-11J.zip",
                ExpectedMd5 = "69bca2fcd4d1c39db0fde85446f33f00",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 5.0.0-11J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.5.0-10E.zip",
                ExpectedMd5 = "61abc811b566764e245e8d35f903bd5b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.5.0-10E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.5.0-10J.zip",
                ExpectedMd5 = "a2ca6884f2f2e8578da8ece5012d45eb",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.5.0-10J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.5.0-9U.zip",
                ExpectedMd5 = "b7e8074ea0a9501315b064a1a4e07e4d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.5.0-9U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.4.0-10E.zip",
                ExpectedMd5 = "5404fc2b931518599a36d7895cadf77d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.4.0-10E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.3.0-10E.zip",
                ExpectedMd5 = "44f48accd50196222f2e1c6e7a293182",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.3.0-10E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.2.0-9U.zip",
                ExpectedMd5 = "2df1d9f8b03359eb6afa476b5adb1f33",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.2.0-9U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.2.0-9E.zip",
                ExpectedMd5 = "8462d30dad6b46bf5ad5b67c9ed88b4d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.2.0-9E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.1.0-8E.zip",
                ExpectedMd5 = "a8d80daff9105f86173ed455724df6d2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.1.0-8E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "4.0.0-7E.zip",
                ExpectedMd5 = "daee92dcd4eabc1840977bd3a665d783",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 4.0.0-7E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "3.0.0-6E.zip",
                ExpectedMd5 = "1f2502acfe69314a6a96fb65ca2c789a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 3.0.0-6E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "2.1.0-3E.zip",
                ExpectedMd5 = "9590ea515c3483d9d0a07ec643e76db9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 2.1.0-3E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "2.0.0-2E.zip",
                ExpectedMd5 = "5eaa3bad4ffd8076a4e55740048ac178",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 2.0.0-2E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-0U.zip",
                ExpectedMd5 = "c3689a8bdd9ca2308e9abd5a3a49692b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-0U (USA)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-0E.zip",
                ExpectedMd5 = "3495f4c8d8518f5a3d86f0ad77a632c5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-0E (Europe)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-0J.zip",
                ExpectedMd5 = "e4a792dcece0edb951fb4dc4273473d6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-0J (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.1.0-1E(CFA).zip",
                ExpectedMd5 = "96674bba80871608048fc3d60e2d3c67",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.1.0-1E(CFA) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20U(Without MSET,TWL,WhiteList,AGB).zip",
                ExpectedMd5 = "361b887b2127c0cf420da26da697809d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20U(Without MSET,TWL,WhiteList,AGB) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20E (Without MSET,TWL,WhiteList,AGB).zip",
                ExpectedMd5 = "843051c516cb61ccafcb077733560aea",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20E (Without MSET,TWL,WhiteList,AGB) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-1U(CFA).zip",
                ExpectedMd5 = "c0fb6af2b6bbb82add6b51b3d1fd7037",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-1U(CFA) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-1E(CFA).zip",
                ExpectedMd5 = "9f11c91be3a695587ad8d8af38c0d206",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-1E(CFA) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "1.0.0-1U(Game Update).zip",
                ExpectedMd5 = "b42d7828593a8b885ba39f25417dd4d7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 1.0.0-1U(Game Update) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "9.2.0-20J(Without MSET,TWL,WhiteList,AGB).zip",
                ExpectedMd5 = "9b6a4fd6215242d75b8940b235caf5f2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 9.2.0-20J(Without MSET,TWL,WhiteList,AGB) (Japan)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3ds",
                FileName = "2.2.0 (CFA).zip",
                ExpectedMd5 = "ac14f6e224b62845f5ee0c3f3abc8ad1",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo 3DS Official Firmware 2.2.0 (CFA) (Japan)",
                IsRequired = false
            },

            // Nintendo Switch
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 23.0.1.zip",
                ExpectedMd5 = "910bd0b523553c2a97254b654392976d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 23.0.1",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 23.0.0.zip",
                ExpectedMd5 = "e9f3a9cdf8d545ab97c29f2f0d33a512",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 23.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 22.5.0.zip",
                ExpectedMd5 = "1b18e881a68d3a41f35c1de9d0ccf3a2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 22.5.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 22.1.0.zip",
                ExpectedMd5 = "0bb4ae3b0a332ab5a4dabc9ad3786958",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 22.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 22.0.0.zip",
                ExpectedMd5 = "6bf0608e502196f874b66ad8ccd8847e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 22.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 21.2.0.zip",
                ExpectedMd5 = "07f406f724ffb7a5f5e2561b59470167",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 21.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 21.1.0.zip",
                ExpectedMd5 = "d46f553376a9cc6af3b9eead5b10b491",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 21.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 21.0.1.zip",
                ExpectedMd5 = "63bbc58f84c415771688255def2018ac",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 21.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 21.0.0.zip",
                ExpectedMd5 = "b525fe3937ecc6cd2e1646df48993d8e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 21.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.5.0.zip",
                ExpectedMd5 = "21dffefa931f1a41c4257579471cd9c6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.5.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.4.0.zip",
                ExpectedMd5 = "f8fbf127335bd1800c826ff6b14badb2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.4.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.3.0.zip",
                ExpectedMd5 = "a0fddca9aa7fa540c1f6b1f291a4b27f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.3.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.2.0.zip",
                ExpectedMd5 = "98f5fd95cecd06555229834537e4d923",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.1.5.zip",
                ExpectedMd5 = "1bbc6461e96f24d0ea858a8c031bafff",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.1.5",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.1.1.zip",
                ExpectedMd5 = "1727c9e364ed61e6397564804bce10d5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.1.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.1.0.zip",
                ExpectedMd5 = "4d1c20eb807813c6838fc0732cb6452e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.0.1.zip",
                ExpectedMd5 = "552422f6fcb135b47a1250c48ae5a2d7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 20.0.0.zip",
                ExpectedMd5 = "674d839e3022d70e75800c822c8e8466",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 20.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.1 (Rebootless Update).zip",
                ExpectedMd5 = "ca9f691174ed43551c6d1f847da7fc1d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.1 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.1.zip",
                ExpectedMd5 = "c9861078638a8f90e676e15db0bbe0f2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.0.zip",
                ExpectedMd5 = "2ce401a4cdee011824c2487e0f1446a9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.1.0 (Rebootless Update).zip",
                ExpectedMd5 = "1b6f9511eda05c90b9df325413e37621",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.1.0 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.1.0.zip",
                ExpectedMd5 = "dfd91e2bc34ebf81897a2c65723ee396",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.0.1.zip",
                ExpectedMd5 = "70b1e47e6148974e6f35ccb32042d80e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.0.0.zip",
                ExpectedMd5 = "8dbacdbaa4e90be98ed0706f7e90a241",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.1 (Rebootless Update).zip",
                ExpectedMd5 = "5a56b448fcdf173aa0785ee95c3bbdad",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.1 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.1.zip",
                ExpectedMd5 = "107f55a13e35efc95c27eca693f93ab7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.0 (Rebootless Update).zip",
                ExpectedMd5 = "7b6e528486a013b035d9fbb4bd32b15e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.0 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.0.zip",
                ExpectedMd5 = "907f7c99ba54ff84aba5dfc9e22df968",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.1.0.zip",
                ExpectedMd5 = "c32db52758a4bf6503869db531012e3d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3 (Rebootless Update 2).zip",
                ExpectedMd5 = "61e55a44e15f33bc79a80388fa82dd8a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3 (Rebootless Update 2)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3 (Rebootless Update).zip",
                ExpectedMd5 = "2c826b4df34a48660d923cc6f47ec949",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3.zip",
                ExpectedMd5 = "ebb152b356b94eb95ed4a5ab9d4145f1",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.2.zip",
                ExpectedMd5 = "462c6a0d29daa4170c37ad1b95899bd5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.1.zip",
                ExpectedMd5 = "cb287286188dc3072352af2bb4830911",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.0.zip",
                ExpectedMd5 = "9feda64cab86f851f1630979ae33a6d5",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.1 (Rebootless Update).zip",
                ExpectedMd5 = "4fe164705b2392592553586f7cf9d03e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.1 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.1.zip",
                ExpectedMd5 = "69735562cebf54ec61aad54cfefc9a5d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.0.zip",
                ExpectedMd5 = "a7023429f85fdd3a40b4661188f5b65a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update 3).zip",
                ExpectedMd5 = "36808cdb78b5986d02817e6667dfe15b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update 3)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update 2).zip",
                ExpectedMd5 = "50f2564ce85a0818942c5fc1db20a89c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update 2)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update).zip",
                ExpectedMd5 = "f21375202bc25d55733a41d7a2d8978c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2.zip",
                ExpectedMd5 = "522d3c4caf11d9874dbd830907f1f36a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.1.zip",
                ExpectedMd5 = "4e864e134318aa80ac06e7a676eb96d8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.0.zip",
                ExpectedMd5 = "778b4e7854afa1a0baa98c44988e68ac",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.0.0.zip",
                ExpectedMd5 = "816010565838f30b047d0059efa8c3ea",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 13.2.1.zip",
                ExpectedMd5 = "881379299c1c9cd2a4b7a90c18c9ea82",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 13.2.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 13.2.0.zip",
                ExpectedMd5 = "f4f0a7e77d39e209d1be0ee8641c9afb",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 13.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 13.1.0.zip",
                ExpectedMd5 = "ab837980ed2c83eedaecb28ebf667d9a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 13.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 13.0.0.zip",
                ExpectedMd5 = "9fa654de1a4682e517a15b5a79a7895d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 13.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 12.1.0.zip",
                ExpectedMd5 = "6c8f353daccf73e1da7ee5dd532656f0",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 12.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 12.0.3.zip",
                ExpectedMd5 = "35cb46f7d2d609f7d056c22099d1c469",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 12.0.3",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 12.0.2.zip",
                ExpectedMd5 = "939ec032227741b60aafacbedc4e476f",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 12.0.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 12.0.1.zip",
                ExpectedMd5 = "12040ddd1533cf58ff2fb690d7bec3ee",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 12.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 12.0.0.zip",
                ExpectedMd5 = "d297bdb5a6d0341df6cc24195f604abe",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 12.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.1 (Rebootless Update).zip",
                ExpectedMd5 = "53f68ddd2a2f4f79e98edc02ff2ad0bd",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.1 (Rebootless Update)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.1.zip",
                ExpectedMd5 = "73200c7e53c864f06f648c121ee78677",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.0.zip",
                ExpectedMd5 = "7330b3c560f80caeb2fa3d831d5203f2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.2.0.zip",
                ExpectedMd5 = "8c18c4e5908178f9af4d8aad183b8ca2",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.1.0.zip",
                ExpectedMd5 = "a2ccac10398351ea0f9d3af30534297c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.0.4.zip",
                ExpectedMd5 = "9816b980f84b1cef8a91f5ce3a697678",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.0.4",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.0.3.zip",
                ExpectedMd5 = "f15f82570701868246016616979147d8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.0.3",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.0.2.zip",
                ExpectedMd5 = "4b795c906abf70439fe53ee4a6fc636c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.0.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.0.1.zip",
                ExpectedMd5 = "ddaec6ef4674887c53b9f153eda7e9b6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.0.0.zip",
                ExpectedMd5 = "ca88f99be9fadc0088210ef9a4b3986a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 9.2.0.zip",
                ExpectedMd5 = "64ffa81de3a4b7bda973036520113cda",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 9.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 9.1.0.zip",
                ExpectedMd5 = "b0ed3ce315918e05bccbf31694d52584",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 9.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 9.0.1.zip",
                ExpectedMd5 = "6c428005c384072e32dcf0bddf971817",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 9.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 9.0.0.zip",
                ExpectedMd5 = "0ec9d07322ae98fc1163674d40437305",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 9.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 8.1.1.zip",
                ExpectedMd5 = "4fc67d5be86e4880612fbca76f6d8e70",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 8.1.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 8.1.0.zip",
                ExpectedMd5 = "85907ab512b028d6a81a2e1b414b59b9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 8.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 8.0.1.zip",
                ExpectedMd5 = "1bd7a32f9a2a67ffda6a245f76f06609",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 8.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 8.0.0.zip",
                ExpectedMd5 = "e1641978fc281f3ce6b033398ae520dd",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 8.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 7.0.1.zip",
                ExpectedMd5 = "589ab86da5f6028a7f3d39e85e043d4a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 7.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 7.0.0.zip",
                ExpectedMd5 = "7faf4c721a96767f11ddf83d1ea06b4a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 7.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 6.2.0.zip",
                ExpectedMd5 = "30e4a67c2943465c5441f203bd169ba8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 6.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 6.1.0.zip",
                ExpectedMd5 = "0547222834a00891ed82d4f58d1b2c7b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 6.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 6.0.1.zip",
                ExpectedMd5 = "684f2184d9dd4bdc25ff161e0ea7353d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 6.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 6.0.0.zip",
                ExpectedMd5 = "8e107ad46a5aacc1f8f5db7fc83d6945",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 6.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 6.0.0 (Pre-Release).zip",
                ExpectedMd5 = "fcea05e3e2743464db57b031c5eb334a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 6.0.0 (Pre-Release)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 5.1.0.zip",
                ExpectedMd5 = "656823850a70fb3050079423ee177c1a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 5.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 5.0.2.zip",
                ExpectedMd5 = "f14d2064255517aa1383e7e468e2ef19",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 5.0.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 5.0.1.zip",
                ExpectedMd5 = "79c2ac770eece2f9b713f3c6b12cc19a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 5.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 5.0.0.zip",
                ExpectedMd5 = "de25e742c8c0fa9c7f6d079f65ddbf92",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 5.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 4.1.0.zip",
                ExpectedMd5 = "fe53dd1eaa323bd9003f75a96822cb31",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 4.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 4.0.1.zip",
                ExpectedMd5 = "3680ddfb0f7a2f01c83495dca909c757",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 4.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 4.0.0.zip",
                ExpectedMd5 = "3cde6a57ebf6c61e8fc8e858cf7614da",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 4.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 3.0.2.zip",
                ExpectedMd5 = "521e4aabc0b3b18d49f99c861cd55196",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 3.0.2",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 3.0.1.zip",
                ExpectedMd5 = "a245bc7d3151cd51687ce602a1d4dfbb",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 3.0.1",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 3.0.0.zip",
                ExpectedMd5 = "f4b5f8bffded14cca836bc24ecf19c06",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 3.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 2.3.0.zip",
                ExpectedMd5 = "91e06f945e00e6412cc4ac44a0faa72b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 2.3.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 2.2.0.zip",
                ExpectedMd5 = "137f5d416d1b41ad26c35575dd534bc4",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 2.2.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 2.1.0.zip",
                ExpectedMd5 = "35752c26badc270f9fdd51883922432b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 2.1.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 2.0.0.zip",
                ExpectedMd5 = "78fe09c2da202d35e58c7b07bb7f39a8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 2.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 1.0.0.zip",
                ExpectedMd5 = "46e6814359631d3c92bc43ead4328349",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 1.0.0",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 1.0.0-7 (Pre-Release).zip",
                ExpectedMd5 = "46e02d9319597d61311bc42de2db9473",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 1.0.0-7 (Pre-Release)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.2 (China).zip",
                ExpectedMd5 = "f282aae7e3a7d4ee1590c2b259a06c06",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.2 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.1 (Rebootless Update) (China).zip",
                ExpectedMd5 = "228d9b9a565af8b20c0ab4b582eec82d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.1 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.1 (China).zip",
                ExpectedMd5 = "638046b45032949d08bb331ddd143d7a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 19.0.0 (China).zip",
                ExpectedMd5 = "957ac3cbe41f5add267b8b5abb070bd7",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 19.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.1.0 (Rebootless Update) (China).zip",
                ExpectedMd5 = "cecc57a519d9dd09747705a6db97b4ec",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.1.0 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.1.0 (China).zip",
                ExpectedMd5 = "ea43329a487fa14b1e79e917982466da",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.1.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.0.1 (China).zip",
                ExpectedMd5 = "2ec5e38ad97f851918f7c49b18e0d941",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 18.0.0 (China).zip",
                ExpectedMd5 = "e3a58a253414b9512b1d6842af28da6b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 18.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.1 (Rebootless Update) (China).zip",
                ExpectedMd5 = "8aa43f6f9e49d10d2032f091deadaa93",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.1 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.1 (China).zip",
                ExpectedMd5 = "c5ad2549506279626129f8e4324681c1",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.0 (Rebootless Update) (China).zip",
                ExpectedMd5 = "32802ffee5a4621c71c2d6d2a423f1e9",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.0 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 17.0.0 (China).zip",
                ExpectedMd5 = "8c66717212e40aed2258cd422081a36a",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 17.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.1.0 (China).zip",
                ExpectedMd5 = "acce115d3419cae4a38f9016b7f96a5c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.1.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3 (Rebootless Update 2) (China).zip",
                ExpectedMd5 = "1e3d8d2973dd11b7476da8d05da76d09",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3 (Rebootless Update 2) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3 (Rebootless Update) (China).zip",
                ExpectedMd5 = "5cfe2aec7345cc4621568bd1f4bd7b0c",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.3 (China).zip",
                ExpectedMd5 = "890ff0985677ed2dc173d225458202fe",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.3 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.2 (China).zip",
                ExpectedMd5 = "85049020f42bc88693c9b2004d79f251",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.2 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.1 (China).zip",
                ExpectedMd5 = "4af4ec5e910f97dcffcfba69b8ac41b6",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 16.0.0 (China).zip",
                ExpectedMd5 = "3b53ff102eadaec61e4fd61c2a4445bf",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 16.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.1 (Rebootless Update) (China).zip",
                ExpectedMd5 = "16e7357fad623d34eb323e69ddf3bbf1",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.1 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.1 (China).zip",
                ExpectedMd5 = "271d4ddcfe93e3847d7c25bc5bb9908e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 15.0.0 (China).zip",
                ExpectedMd5 = "64a061e85e334edbb7682504463e0872",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 15.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update 3) (China).zip",
                ExpectedMd5 = "ff9d310500684e1763bae5efbf374d27",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update 3) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update 2) (China).zip",
                ExpectedMd5 = "dbf45b7f463f2d6476f65924efe98644",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update 2) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (Rebootless Update) (China).zip",
                ExpectedMd5 = "0494d028cedd3a039524c5514378c64b",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.2 (China).zip",
                ExpectedMd5 = "bbefda8a9d00fc8a53800b4154d14e4d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.2 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.1 (China).zip",
                ExpectedMd5 = "da606c3b1ef83921ab9c84d37e2ee3a8",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.1.0 (China).zip",
                ExpectedMd5 = "e9d84226f636493a1628e73316bae14e",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.1.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 14.0.0 (China).zip",
                ExpectedMd5 = "01564aee6630d6802645c99bad3ede21",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 14.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 13.2.1 (China).zip",
                ExpectedMd5 = "3d73a62a57a3848a2fe8c95d88eb0371",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 13.2.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.1 (Rebootless Update) (China).zip",
                ExpectedMd5 = "87044a53da90e4c35e05873176e68ad0",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.1 (Rebootless Update) (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.1 (China).zip",
                ExpectedMd5 = "ca2e74a837e8eec7c1ccfc78c013de09",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.1 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 11.0.0 (China).zip",
                ExpectedMd5 = "1c6e685b4a83b25ef38e06faa32b308d",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 11.0.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.2.0 (China).zip",
                ExpectedMd5 = "0a50dc5b55625595c36bab31aa677035",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.2.0 (China)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "switch",
                FileName = "Firmware 10.1.1 (China).zip",
                ExpectedMd5 = "ed43634e35af0c605ffed94cfde0f3fc",
                ExpectedSha1 = string.Empty,
                Description = "Nintendo Switch Official Firmware 10.1.1 (China)",
                IsRequired = false
            },

            // Nintendo Pokémon Mini & GameCube
            new BiosFile
            {
                GamePlatform = "pokemonmini",
                FileName = "bios.min",
                ExpectedMd5 = "1e4fb124a3a886865acb574f388c803d",
                ExpectedSha1 = "daad4113713ed776fbd47727762bca81ba74915f",
                Description = "Pokémon Mini Internal BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "gamecube",
                FileName = "IPL.bin",
                ExpectedMd5 = "fae2b558ffc344467170520d62177e5c",
                ExpectedSha1 = "015808f637a984acde6a06efa7546e278293c6ee",
                Description = "Nintendo GameCube Initial Program Loader (NTSC-U)",
                IsRequired = false
            },

            // NEC TurboGrafx-CD / PC Engine CD & PC-FX
            new BiosFile
            {
                GamePlatform = "tgcd",
                FileName = "syscard3.pce",
                ExpectedMd5 = "38179df8f4ac870017db21ebcbf53114",
                ExpectedSha1 = "79f5ff55dd10187c7fd7b8daab0b3ffbd1f56a2c",
                Description = "PC Engine Super CD-ROM2 System v3.0",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "pcfx",
                FileName = "pcfx.rom",
                ExpectedMd5 = "08e36edbea28a017f79f8d4f7ff9b6d7",
                ExpectedSha1 = "1a77fd83e337f906aecab27a1604db064cf10074",
                Description = "NEC PC-FX System BIOS v1.00",
                IsRequired = true
            },

            // SNK Neo Geo AES/MVS & Neo Geo CD
            new BiosFile
            {
                GamePlatform = "neogeo",
                FileName = "neogeo.zip",
                ExpectedMd5 = "00dad01abdbf8ea9e79ad2fe11bdb182",
                ExpectedSha1 = "deb62b0074b8cae4f162c257662136733cfc76ad",
                Description = "SNK Neo Geo AES / MVS System BIOS Pack (UniBIOS / MVS)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "neogeocd",
                FileName = "neocd_z.rom",
                ExpectedMd5 = "11526d58d4c524daef7d5d677dc6b004",
                ExpectedSha1 = "b0f1c4fa8d4492a04431805f6537138b842b549f",
                Description = "SNK Neo Geo CDZ System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "neogeocd",
                FileName = "000-lo.lo",
                ExpectedMd5 = "fc7599f3f871578fe9a0453662d1c966",
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
                ExpectedSha1 = "3c912300775d1ad730dc35757e279c274c0acaad",
                Description = "Panasonic FZ-10 3DO REAL BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "3do",
                FileName = "panafz1.bin",
                ExpectedMd5 = "f47264dd47fe30f73ab3c010015c155b",
                ExpectedSha1 = "34bf189111295f74d7b7dfc1f304d98b8d36325a",
                Description = "Panasonic FZ-1 3DO REAL BIOS",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "3do",
                FileName = "goldstar.bin",
                ExpectedMd5 = "8639fd5e549bd6238cfee79e3e749114",
                ExpectedSha1 = "c4a2e5336f77fb5f743de1eea2cda43675ee2de7",
                Description = "GoldStar GDO-101M 3DO BIOS",
                IsRequired = false
            },

            // Atari 5200, 7800, Lynx, Jaguar CD
            new BiosFile
            {
                GamePlatform = "atari5200",
                FileName = "5200.rom",
                ExpectedMd5 = "281f20ea4320404ec820fb7ec0693b38",
                ExpectedSha1 = "6ad7a1e8c9fad486fbec9498cb48bf5bc3adc530",
                Description = "Atari 5200 SuperSystem BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "atari7800",
                FileName = "7800 BIOS (U).rom",
                ExpectedMd5 = "0763f1ffb006ddbe32e52d497ee848ae",
                ExpectedSha1 = "d9d134bb6b36907c615a594cc7688f7bfcef5b43",
                Description = "Atari 7800 ProSystem BIOS (NTSC)",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "lynx",
                FileName = "lynxboot.img",
                ExpectedMd5 = "fcd403db69f54290b51035d82f835e7b",
                ExpectedSha1 = "e4ed47fae31693e016b081c6bda48da5b70d7ccb",
                Description = "Atari Lynx Boot Image",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "jaguarcd",
                FileName = "jagcd.rom",
                ExpectedMd5 = "77cd95c7ad06a39f4c59995094aa10f9",
                ExpectedSha1 = "73883e7a6e9b132452436f7ab1aeaeb0776428e5",
                Description = "Atari Jaguar CD Boot ROM",
                IsRequired = true
            },

            // ColecoVision, Intellivision, Odyssey 2, Vectrex, Channel F
            new BiosFile
            {
                GamePlatform = "colecovision",
                FileName = "coleco.rom",
                ExpectedMd5 = "2c66f5911e5b42b8ebe113403548eee7",
                ExpectedSha1 = "45bedc4cbdeac66c7df59e9e599195c778d86a92",
                Description = "ColecoVision System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "intellivision",
                FileName = "exec.bin",
                ExpectedMd5 = "62e761035cb657903761800f4437b8af",
                ExpectedSha1 = "5a65b922b562cb1f57dab51b73151283f0e20c7a",
                Description = "Intellivision Executive ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "intellivision",
                FileName = "grom.bin",
                ExpectedMd5 = "0cd5946c6473e42e8e4c2137785e427f",
                ExpectedSha1 = "f9608bb4ad1cfe3640d02844c7ad8e0bcd974917",
                Description = "Intellivision Graphics ROM (GROM)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "odyssey2",
                FileName = "o2rom.bin",
                ExpectedMd5 = "562d5ebf9e030a40d6fabfc2f33139fd",
                ExpectedSha1 = "b2e1955d957a475de2411770452eff4ea19f4cee",
                Description = "Magnavox Odyssey² / Philips VideopacG7000 BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "vectrex",
                FileName = "fastboot.vec",
                ExpectedMd5 = "ba9260bb961d60e718378cc16a8051d7",
                ExpectedSha1 = "b9bbf5bb0eac52d039a4a993a2d8064b862c9e28",
                Description = "GCE Vectrex Fast-Boot Executive ROM",
                IsRequired = false
            },
            new BiosFile
            {
                GamePlatform = "channelf",
                FileName = "sl31253.bin",
                ExpectedMd5 = "ac9804d4c0e9d07e33472e3726ed15c3",
                ExpectedSha1 = "81193965a374d77b99b4743d317824b53c3e3c78",
                Description = "Fairchild Channel F PSU ROM (SL31253)",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "channelf",
                FileName = "sl31254.bin",
                ExpectedMd5 = "da98f4bb3242ab80d76629021bb27585",
                ExpectedSha1 = "8f70d1b74483ba3a37e86cf16c849d601a8c3d2c",
                Description = "Fairchild Channel F PSU ROM (SL31254)",
                IsRequired = true
            },

            // MSX / Amiga / Sharp X68000 / NEC PC-98 / Philips CD-i
            new BiosFile
            {
                GamePlatform = "msx",
                FileName = "MSX.ROM",
                ExpectedMd5 = "aa95aea2563cd5ec0a0919b44cc17d47",
                ExpectedSha1 = "409e82adac40f6bdd18eb6c84e8b2fbdc7fb5498",
                Description = "MSX1 System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "msx",
                FileName = "MSX2.ROM",
                ExpectedMd5 = "ec3a01c91f24fbddcbcab0ad301bc9ef",
                ExpectedSha1 = "6103b39f1e38d1aa2d84b1c3219c44f1abb5436e",
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
                ExpectedSha1 = "0ed038ed2133b9f78c6e37256807424e0d927560",
                Description = "Sharp X68000 IPL ROM v1.0",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "x68000",
                FileName = "cgrom.dat",
                ExpectedMd5 = "cb0a5cfcf7247a7eab74bb2716260269",
                ExpectedSha1 = "8d72c5b4d63bb14c5dbdac495244d659aa1498b6",
                Description = "Sharp X68000 Character Generator Font ROM",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "pc98",
                FileName = "bios.rom",
                ExpectedMd5 = "cd237e16e7e77c06bb58540e9e9fca68",
                ExpectedSha1 = "76a7360502fa0296ea93b4c537174610a834d367",
                Description = "NEC PC-9801VX System BIOS",
                IsRequired = true
            },
            new BiosFile
            {
                GamePlatform = "cdi",
                FileName = "cdimono1.zip",
                ExpectedMd5 = "3c96160075816b4916f5329b5e539ca0",
                ExpectedSha1 = "36771315484a1f57227e23278df91b84b5d7d3be",
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
        private readonly IIndexerFactory _indexerFactory;

        public BiosService(
            Logger logger,
            ISeriesService seriesService = null,
            IRootFolderService rootFolderService = null,
            IHttpClient httpClient = null,
            IAppFolderInfo appFolderInfo = null,
            IEpisodeRepository episodeRepository = null,
            IHistoryRepository historyRepository = null,
            IGameCatalogService gameCatalogService = null,
            IIndexerFactory indexerFactory = null)
        {
            _logger = logger;
            _seriesService = seriesService;
            _rootFolderService = rootFolderService;
            _httpClient = httpClient;
            _appFolderInfo = appFolderInfo;
            _episodeRepository = episodeRepository;
            _historyRepository = historyRepository;
            _gameCatalogService = gameCatalogService;
            _indexerFactory = indexerFactory;
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
                    string.Equals(Path.GetFileName(f), entry.FileName, StringComparison.OrdinalIgnoreCase) ||
                    (IsCompressedArchive(f) && string.Equals(Path.GetFileNameWithoutExtension(f), Path.GetFileNameWithoutExtension(entry.FileName), StringComparison.OrdinalIgnoreCase)));

                if (matchingFile != null)
                {
                    entry.ResolvedPath = FormatDisplayPath(matchingFile);
                    var valid = VerifyBios(matchingFile, entry.ExpectedMd5, entry.ExpectedSha1);
                    if (!valid && IsCompressedArchive(matchingFile) && !entry.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetRawPath = Path.Combine(Path.GetDirectoryName(matchingFile), entry.FileName);
                        valid = TryExtractBiosFromArchive(matchingFile, targetRawPath, entry);
                        if (valid)
                        {
                            entry.ResolvedPath = FormatDisplayPath(targetRawPath);
                        }
                    }

                    if (valid)
                    {
                        entry.IsVerified = true;
                        entry.Status = 1;
                    }
                    else
                    {
                        _logger.Warn("BIOS file {0} at {1} failed hash verification. Discarding invalid file and replacing with verified copy.", entry.FileName, matchingFile);
                        try
                        {
                            File.Delete(matchingFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to discard invalid BIOS file at {0}", matchingFile);
                        }

                        var targetDir = Path.GetDirectoryName(matchingFile);
                        var replaced = TryDownloadAndVerifyBios(entry, targetDir, platformSlug);
                        if (replaced)
                        {
                            entry.IsVerified = true;
                            entry.Status = 1;
                        }
                        else
                        {
                            entry.IsVerified = false;
                            entry.Status = 2;
                        }
                    }
                }
                else
                {
                    var baseName = Path.GetFileNameWithoutExtension(entry.FileName);
                    var candidateArchive = existingFiles.FirstOrDefault(f =>
                        string.Equals(Path.GetFileName(f), $"{entry.FileName}.zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(f), $"{baseName}.zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(f), $"{entry.FileName}.7z", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(f), $"{baseName}.7z", StringComparison.OrdinalIgnoreCase));

                    if (candidateArchive != null)
                    {
                        var targetExtractPath = Path.Combine(Path.GetDirectoryName(candidateArchive), entry.FileName);
                        if (TryExtractBiosFromArchive(candidateArchive, targetExtractPath, entry))
                        {
                            entry.ResolvedPath = FormatDisplayPath(targetExtractPath);
                            entry.IsVerified = true;
                            entry.Status = 1;
                        }
                    }
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
                var targetDir = ResolveTargetBiosDirectory(entry.GamePlatform, platformSlug);
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    targetDir = Path.Combine(
                        _appFolderInfo?.AppDataFolder ?? Path.GetTempPath(),
                        "bios",
                        entry.GamePlatform);
                }

                TryDownloadAndVerifyBios(entry, targetDir, platformSlug);
            }

            return GetCatalog(platformSlug);
        }

        public bool IsKnownBiosFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var cleanName = Path.GetFileName(fileName);
            return CanonicalBiosCatalog.Any(b => string.Equals(b.FileName, cleanName, StringComparison.OrdinalIgnoreCase)) ||
                   AlternateBiosHashesByFileName.ContainsKey(cleanName);
        }

        public bool TryGetBiosFile(string fileName, out BiosFile biosFile, string platformSlug = null)
        {
            biosFile = null;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var cleanName = Path.GetFileName(fileName);
            var normalizedGroup = string.IsNullOrWhiteSpace(platformSlug) ? null : ResolveBiosGroupKey(platformSlug);

            var candidate = CanonicalBiosCatalog.FirstOrDefault(b =>
                string.Equals(b.FileName, cleanName, StringComparison.OrdinalIgnoreCase) &&
                (normalizedGroup == null || string.Equals(b.GamePlatform, normalizedGroup, StringComparison.OrdinalIgnoreCase)));

            if (candidate != null)
            {
                biosFile = CloneBiosFile(candidate);
                return true;
            }

            if (AlternateBiosHashesByFileName.ContainsKey(cleanName))
            {
                var altMatch = CanonicalBiosCatalog.FirstOrDefault(b =>
                    string.Equals(b.FileName, cleanName, StringComparison.OrdinalIgnoreCase));
                if (altMatch != null)
                {
                    biosFile = CloneBiosFile(altMatch);
                    return true;
                }
            }

            return false;
        }

        public bool ProcessCandidateBiosFile(string sourceFilePath, Series series, bool move = false)
        {
            if (series == null || string.IsNullOrWhiteSpace(series.Path) || string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return false;
            }

            if (!File.Exists(sourceFilePath))
            {
                return false;
            }

            var fileName = Path.GetFileName(sourceFilePath);
            if (!TryGetBiosFile(fileName, out var biosFile, series.TitleSlug))
            {
                return false;
            }

            var isValid = VerifyBios(sourceFilePath, biosFile.ExpectedMd5, biosFile.ExpectedSha1);
            if (!isValid)
            {
                _logger.Warn("Candidate BIOS file {0} at {1} failed cryptographic verification", fileName, sourceFilePath);
                return false;
            }

            var biosDir = Path.Combine(series.Path, "BIOS");
            Directory.CreateDirectory(biosDir);
            var destinationPath = Path.Combine(biosDir, biosFile.FileName);

            if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("Verified existing BIOS file {0} for platform {1}", biosFile.FileName, series.Title);
                return true;
            }

            try
            {
                if (move)
                {
                    File.Move(sourceFilePath, destinationPath, true);
                }
                else
                {
                    File.Copy(sourceFilePath, destinationPath, true);
                }

                var verified = VerifyBios(destinationPath, biosFile.ExpectedMd5, biosFile.ExpectedSha1);
                if (verified)
                {
                    _logger.Info("Successfully {0} and verified BIOS file {1} to {2}", move ? "imported" : "copied", biosFile.FileName, destinationPath);
                    var fileLength = new FileInfo(destinationPath).Length;
                    RecordBiosDownloadHistory(biosFile, series.TitleSlug, destinationPath, $"file://{sourceFilePath}", fileLength);
                    return true;
                }

                File.Delete(destinationPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to {0} BIOS file {1} to {2}", move ? "move" : "copy", sourceFilePath, destinationPath);
                return false;
            }
        }

        public void ScanAndVerifyPlatformBios(Series series)
        {
            if (series == null || string.IsNullOrWhiteSpace(series.Path) || !Directory.Exists(series.Path))
            {
                return;
            }

            var biosDir = Path.Combine(series.Path, "BIOS");
            if (!Directory.Exists(biosDir))
            {
                Directory.CreateDirectory(biosDir);
            }

            try
            {
                var looseFiles = Directory.GetFiles(series.Path, "*.*", SearchOption.AllDirectories);
                foreach (var file in looseFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (IsKnownBiosFileName(fileName))
                    {
                        var dir = Path.GetDirectoryName(file);
                        if (!string.Equals(Path.GetFullPath(dir ?? string.Empty), Path.GetFullPath(biosDir), StringComparison.OrdinalIgnoreCase))
                        {
                            ProcessCandidateBiosFile(file, series, move: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error checking loose BIOS files in {0}", series.Path);
            }

            ScanAndVerifyBiosDirectory(biosDir, series.TitleSlug);
        }

        private bool TryDownloadAndVerifyBios(BiosFile entry, string targetDir, string platformSlug = null)
        {
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                return false;
            }

            var destinationPath = Path.Combine(targetDir, entry.FileName);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Directory.CreateDirectory(targetDir);

                if (File.Exists(destinationPath))
                {
                    if (VerifyBios(destinationPath, entry.ExpectedMd5, entry.ExpectedSha1))
                    {
                        entry.ResolvedPath = FormatDisplayPath(destinationPath);
                        entry.IsVerified = true;
                        entry.Status = 1;
                        return true;
                    }

                    _logger.Warn("Discarding invalid BIOS file at {0} before acquiring replacement", destinationPath);
                    try
                    {
                        File.Delete(destinationPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to delete invalid BIOS file at {0}", destinationPath);
                    }
                }

                if (_httpClient == null)
                {
                    return false;
                }

                var candidateUrls = BuildCandidateDownloadUrls(entry);
                var s3AuthHeader = GetInternetArchiveAuthHeader();
                var downloaded = false;
                var usedUrl = candidateUrls.FirstOrDefault() ?? string.Empty;

                InternetArchive.TrackTransferState(
                    destinationPath,
                    $"[BIOS] {entry.FileName} ({entry.PlatformTitle})",
                    destinationPath,
                    524288L,
                    0L,
                    0d,
                    DownloadItemStatus.Downloading);

                var triedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool TryDownloadFromUrls(IEnumerable<string> urlsToTry)
                {
                    foreach (var url in urlsToTry)
                    {
                        if (string.IsNullOrWhiteSpace(url) || !triedUrls.Add(url))
                        {
                            continue;
                        }

                        try
                        {
                            var request = new HttpRequest(url)
                            {
                                SuppressHttpError = true,
                                AllowAutoRedirect = true,
                                RequestTimeout = TimeSpan.FromSeconds(180)
                            };

                            if (!string.IsNullOrWhiteSpace(s3AuthHeader) &&
                                url.Contains("archive.org", StringComparison.OrdinalIgnoreCase))
                            {
                                request.Headers["Authorization"] = s3AuthHeader;
                            }

                            var response = _httpClient.Get(request);
                            if (response != null && !response.HasHttpError && response.ResponseData != null && response.ResponseData.Length > 0)
                            {
                                var tempFile = Path.Combine(Path.GetTempPath(), $"bios_dl_{Guid.NewGuid():N}.tmp");
                                try
                                {
                                    File.WriteAllBytes(tempFile, response.ResponseData);

                                    var valid = false;
                                    var targetIsArchive = entry.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                                    if (targetIsArchive)
                                    {
                                        File.Copy(tempFile, destinationPath, true);
                                        valid = VerifyBios(destinationPath, entry.ExpectedMd5, entry.ExpectedSha1);
                                        if (!valid && IsCompressedArchive(tempFile))
                                        {
                                            valid = TryExtractBiosFromArchive(tempFile, destinationPath, entry);
                                        }
                                    }
                                    else if (IsCompressedArchive(tempFile) || url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                                    {
                                        valid = TryExtractBiosFromArchive(tempFile, destinationPath, entry);
                                    }
                                    else
                                    {
                                        File.Copy(tempFile, destinationPath, true);
                                        valid = VerifyBios(destinationPath, entry.ExpectedMd5, entry.ExpectedSha1);
                                    }

                                    if (valid)
                                    {
                                        usedUrl = url;
                                        return true;
                                    }

                                    _logger.Warn("Downloaded BIOS file {0} from {1} does not match expected hashes. Discarding invalid payload.", entry.FileName, url);
                                    if (File.Exists(destinationPath))
                                    {
                                        try
                                        {
                                            File.Delete(destinationPath);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.Error(ex, "Failed to delete invalid downloaded BIOS file at {0}", destinationPath);
                                        }
                                    }
                                }
                                finally
                                {
                                    if (File.Exists(tempFile))
                                    {
                                        try
                                        {
                                            File.Delete(tempFile);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "Mirror fetch failed for BIOS {0} from {1}", entry.FileName, url);
                            if (File.Exists(destinationPath))
                            {
                                try
                                {
                                    File.Delete(destinationPath);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }

                    return false;
                }

                downloaded = TryDownloadFromUrls(candidateUrls);
                if (!downloaded)
                {
                    var dynamicUrls = DiscoverArchiveOrgCandidateUrls(entry, s3AuthHeader);
                    if (dynamicUrls.Count > 0)
                    {
                        downloaded = TryDownloadFromUrls(dynamicUrls);
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

                    entry.ResolvedPath = FormatDisplayPath(destinationPath);
                    entry.IsVerified = true;
                    entry.Status = 1;

                    RecordBiosDownloadHistory(entry, platformSlug, destinationPath, usedUrl, fileLength);
                    InternetArchive.RemoveTrackedTransfer(destinationPath);
                    return true;
                }

                InternetArchive.RemoveTrackedTransfer(destinationPath);
                if (File.Exists(destinationPath))
                {
                    try
                    {
                        File.Delete(destinationPath);
                    }
                    catch
                    {
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                InternetArchive.RemoveTrackedTransfer(destinationPath);
                if (File.Exists(destinationPath))
                {
                    try
                    {
                        File.Delete(destinationPath);
                    }
                    catch
                    {
                    }
                }

                _logger.Warn(ex, "Failed to download replacement BIOS file {0} for {1}", entry.FileName, entry.GamePlatform);
                return false;
            }
        }

        private static bool IsCompressedArchive(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            if (ext is ".zip" or ".gz" or ".tgz" or ".tar" or ".7z" or ".rar")
            {
                return true;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 4)
                {
                    return false;
                }

                var header = new byte[6];
                var read = fs.Read(header, 0, header.Length);
                if (read >= 4)
                {
                    if (header[0] == 0x50 && header[1] == 0x4B &&
                        ((header[2] == 0x03 && header[3] == 0x04) || (header[2] == 0x05 && header[3] == 0x06)))
                    {
                        return true;
                    }

                    if (header[0] == 0x1F && header[1] == 0x8B)
                    {
                        return true;
                    }

                    if (read >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private bool TryExtractBiosFromArchive(string archivePath, string destinationPath, BiosFile entry)
        {
            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"bios_extract_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempExtractDir);
                var extracted = ExtractArchive(archivePath, tempExtractDir);
                if (!extracted)
                {
                    return false;
                }

                var files = Directory.GetFiles(tempExtractDir, "*.*", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    return false;
                }

                foreach (var file in files)
                {
                    if (VerifyBios(file, entry.ExpectedMd5, entry.ExpectedSha1))
                    {
                        var destDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrWhiteSpace(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        File.Copy(file, destinationPath, true);
                        return true;
                    }
                }

                var nameMatch = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), entry.FileName, StringComparison.OrdinalIgnoreCase));

                if (nameMatch != null && VerifyBios(nameMatch, entry.ExpectedMd5, entry.ExpectedSha1))
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(nameMatch, destinationPath, true);
                    return true;
                }

                if (files.Length == 1 && VerifyBios(files[0], entry.ExpectedMd5, entry.ExpectedSha1))
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(files[0], destinationPath, true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to extract BIOS archive from {0}", archivePath);
                return false;
            }
            finally
            {
                if (Directory.Exists(tempExtractDir))
                {
                    try
                    {
                        Directory.Delete(tempExtractDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool ExtractArchive(string archivePath, string destinationDirectory)
        {
            try
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDirectory, true);
                return true;
            }
            catch
            {
            }

            try
            {
                using var fs = File.OpenRead(archivePath);
                var header = new byte[2];
                if (fs.Read(header, 0, 2) == 2 && header[0] == 0x1F && header[1] == 0x8B)
                {
                    fs.Position = 0;
                    using var gz = new GZipStream(fs, CompressionMode.Decompress);
                    var rawName = Path.GetFileNameWithoutExtension(archivePath);
                    if (rawName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                    {
                        rawName = Path.GetFileNameWithoutExtension(rawName);
                    }

                    var outPath = Path.Combine(destinationDirectory, rawName);
                    using var outFs = File.Create(outPath);
                    gz.CopyTo(outFs);
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var tarProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"-xf \"{archivePath}\" -C \"{destinationDirectory}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                tarProcess.Start();
                if (tarProcess.WaitForExit(10000) && tarProcess.ExitCode == 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
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
                var fileName = Path.GetFileName(filePath);
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

                    if ((md5Match && sha1Match) ||
                        IsMatchingAlternateBiosHash(fileName, expectedMd5, expectedSha1, md5Hash, sha1Hash))
                    {
                        _logger.Debug($"BIOS Verified successfully: {filePath}");
                        return true;
                    }

                    // Support 16-bit byte-swapped 128 KB Sega CD / Mega-CD BIOS dumps (e.g. bios_CD_J.bin from RetroPieBIOS)
                    if (stream.Length == 131072 && fileName.StartsWith("bios_CD_", StringComparison.OrdinalIgnoreCase))
                    {
                        stream.Position = 0;
                        var rawBytes = new byte[131072];
                        if (stream.Read(rawBytes, 0, rawBytes.Length) == rawBytes.Length)
                        {
                            for (var i = 0; i < rawBytes.Length - 1; i += 2)
                            {
                                (rawBytes[i], rawBytes[i + 1]) = (rawBytes[i + 1], rawBytes[i]);
                            }

                            var swappedMd5 = BitConverter.ToString(md5.ComputeHash(rawBytes)).Replace("-", "").ToLowerInvariant();
                            var swappedSha1 = BitConverter.ToString(sha1.ComputeHash(rawBytes)).Replace("-", "").ToLowerInvariant();

                            var swappedMd5Match = string.IsNullOrWhiteSpace(expectedMd5) ||
                                                  string.Equals(swappedMd5, expectedMd5, StringComparison.InvariantCultureIgnoreCase);
                            var swappedSha1Match = string.IsNullOrWhiteSpace(expectedSha1) ||
                                                   string.Equals(swappedSha1, expectedSha1, StringComparison.InvariantCultureIgnoreCase);

                            if ((swappedMd5Match && swappedSha1Match) ||
                                IsMatchingAlternateBiosHash(fileName, expectedMd5, expectedSha1, swappedMd5, swappedSha1))
                            {
                                _logger.Debug($"BIOS Verified successfully (16-bit byte-swapped): {filePath}");
                                return true;
                            }
                        }
                    }

                    if (IsCompressedArchive(filePath))
                    {
                        try
                        {
                            var isArchiveCatalogTarget = IsArchiveBiosCatalogEntry(fileName);
                            using var archive = ZipFile.OpenRead(filePath);
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                                {
                                    continue;
                                }

                                using (var entryStream = entry.Open())
                                using (var memStream = new MemoryStream())
                                {
                                    entryStream.CopyTo(memStream);
                                    memStream.Position = 0;

                                    var entryMd5Bytes = md5.ComputeHash(memStream);
                                    var entryMd5 = BitConverter.ToString(entryMd5Bytes).Replace("-", "").ToLowerInvariant();

                                    memStream.Position = 0;
                                    var entrySha1Bytes = sha1.ComputeHash(memStream);
                                    var entrySha1 = BitConverter.ToString(entrySha1Bytes).Replace("-", "").ToLowerInvariant();

                                    var entryMd5Match = string.IsNullOrWhiteSpace(expectedMd5) ||
                                                        string.Equals(entryMd5, expectedMd5, StringComparison.InvariantCultureIgnoreCase);
                                    var entrySha1Match = string.IsNullOrWhiteSpace(expectedSha1) ||
                                                         string.Equals(entrySha1, expectedSha1, StringComparison.InvariantCultureIgnoreCase);

                                    if ((entryMd5Match && entrySha1Match) ||
                                        IsMatchingAlternateBiosHash(fileName, expectedMd5, expectedSha1, entryMd5, entrySha1) ||
                                        IsMatchingAlternateBiosHash(entry.Name, expectedMd5, expectedSha1, entryMd5, entrySha1))
                                    {
                                        _logger.Debug($"BIOS Verified inside archive {filePath}: entry {entry.FullName}");
                                        if (!isArchiveCatalogTarget)
                                        {
                                            try
                                            {
                                                var destDir = Path.GetDirectoryName(filePath);
                                                if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var rawDest = Path.Combine(destDir ?? string.Empty, entry.Name);
                                                    entry.ExtractToFile(rawDest, true);
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        }

                                        return true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
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

        private static bool IsArchiveBiosCatalogEntry(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return CanonicalBiosCatalog.Any(b => string.Equals(b.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsMatchingAlternateBiosHash(
            string fileName,
            string expectedMd5,
            string expectedSha1,
            string actualMd5,
            string actualSha1)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !AlternateBiosHashesByFileName.TryGetValue(fileName, out var alternates))
            {
                return false;
            }

            var catalogEntry = CanonicalBiosCatalog.FirstOrDefault(b =>
                string.Equals(b.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (catalogEntry != null)
            {
                if (!string.IsNullOrWhiteSpace(expectedMd5) &&
                    !string.Equals(expectedMd5, catalogEntry.ExpectedMd5, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(expectedSha1) &&
                    !string.IsNullOrWhiteSpace(catalogEntry.ExpectedSha1) &&
                    !string.Equals(expectedSha1, catalogEntry.ExpectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            foreach (var alt in alternates)
            {
                var md5Match = !string.IsNullOrWhiteSpace(alt.Md5) &&
                               string.Equals(actualMd5, alt.Md5, StringComparison.OrdinalIgnoreCase);
                var sha1Match = string.IsNullOrWhiteSpace(alt.Sha1) ||
                                string.Equals(actualSha1, alt.Sha1, StringComparison.OrdinalIgnoreCase);
                if (md5Match && sha1Match)
                {
                    return true;
                }
            }

            return false;
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
            var baseName = Path.GetFileNameWithoutExtension(entry.FileName);
            var escapedBase = Uri.EscapeDataString(baseName);

            var urls = new List<string>();

            if (!string.IsNullOrWhiteSpace(entry.DownloadUrl))
            {
                urls.Add(entry.DownloadUrl);
            }

            if (AbdessSystemFolderByBiosGroup.TryGetValue(entry.GamePlatform, out var abdessFolder))
            {
                var escapedFolder = Uri.EscapeDataString(abdessFolder);
                urls.Add($"https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/{escapedFolder}/{escapedFile}");
                urls.Add($"https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/{escapedFolder}/{escapedFile}.zip");
            }

            if (string.Equals(entry.FileName, "sgb_bios.bin", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/Nintendo%20-%20Game%20Boy/sgb_bios.bin");
            }
            else if (string.Equals(entry.FileName, "IPL.bin", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/Nintendo%20-%20GameCube%20-%20Wii/GC/USA/IPL.bin");
            }
            else if (string.Equals(entry.GamePlatform, "pc98", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(entry.FileName, "bios.rom", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/NEC%20-%20PC-98/np2kai/bios.rom");
                urls.Add("https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/np2kai/bios.rom");
            }
            else if (string.Equals(entry.FileName, "cdimono1.zip", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add("https://raw.githubusercontent.com/Abdess/retroarch_system/main/bios/Philips%20-%20CD-i/same_cdi/bios/cdimono1.zip");
            }

            urls.Add($"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/{escapedFile}");

            if (string.Equals(entry.GamePlatform, "dreamcast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.GamePlatform, "naomi", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/dc/{escapedFile}");
            }
            else if (string.Equals(entry.GamePlatform, "ps2", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/pcsx2/bios/{escapedFile}");
            }
            else if (string.Equals(entry.GamePlatform, "psp", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/PPSSPP/{escapedFile}");
            }
            else if (string.Equals(entry.FileName, "coleco.rom", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add("https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/colecovision.rom");
            }
            else if (string.Equals(entry.GamePlatform, "x68000", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://raw.githubusercontent.com/archtaurus/RetroPieBIOS/master/BIOS/keropi/{escapedFile}");
            }

            if (AbdessSystemFolderByBiosGroup.TryGetValue(entry.GamePlatform, out var legacyAbdessFolder))
            {
                var escapedFolder = Uri.EscapeDataString(legacyAbdessFolder);
                urls.Add($"https://raw.githubusercontent.com/Abdess/retroarch_system/libretro/{escapedFolder}/{escapedFile}");
                urls.Add($"https://raw.githubusercontent.com/Abdess/retroarch_system/libretro/{escapedFolder}/{escapedFile}.zip");
            }

            urls.Add($"https://archive.org/download/RetroarchSystemFiles/Retroarch-System/{escapedFile}");
            urls.Add($"https://archive.org/download/RetroarchSystemFiles/Retroarch-System/{escapedFile}.zip");
            urls.Add($"https://archive.org/download/RetroarchSystemFiles/Retroarch-System/{escapedBase}.zip");
            urls.Add($"https://archive.org/download/retroarch_bios/{escapedFile}");
            urls.Add($"https://archive.org/download/retroarch_bios/{escapedFile}.zip");
            urls.Add($"https://archive.org/download/retroarch_bios/{escapedBase}.zip");
            urls.Add($"https://archive.org/download/retroarch_bios/system/{escapedFile}");
            urls.Add($"https://archive.org/download/retroarch_bios/system/{escapedFile}.zip");
            urls.Add($"https://archive.org/download/github.com-BatoceraPLUS-Batocera.PLUS-UPDATE-bios_-_2023-08-09_14-37-07/bios/{escapedFile}");

            if (string.Equals(entry.GamePlatform, "dreamcast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.GamePlatform, "naomi", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://archive.org/download/github.com-BatoceraPLUS-Batocera.PLUS-UPDATE-bios_-_2023-08-09_14-37-07/bios/dc/{escapedFile}");
            }
            else if (string.Equals(entry.GamePlatform, "ps2", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://archive.org/download/github.com-BatoceraPLUS-Batocera.PLUS-UPDATE-bios_-_2023-08-09_14-37-07/bios/ps2/{escapedFile}");
            }
            else if (string.Equals(entry.GamePlatform, "ps3", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add($"https://archive.org/download/PS3UPDAT-PUP/{escapedFile}");
                urls.Add($"https://archive.org/download/PS3SOFTWARE/{escapedFile}");
            }

            return urls.Distinct().ToList();
        }

        private string GetInternetArchiveAuthHeader()
        {
            try
            {
                var definitions = _indexerFactory?.All();
                if (definitions == null)
                {
                    return null;
                }

                foreach (var def in definitions)
                {
                    if (def.Settings is InternetArchiveIndexerSettings iaSettings &&
                        !string.IsNullOrWhiteSpace(iaSettings.S3AccessKey) &&
                        !string.IsNullOrWhiteSpace(iaSettings.S3SecretKey))
                    {
                        return $"LOW {iaSettings.S3AccessKey.Trim()}:{iaSettings.S3SecretKey.Trim()}";
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private List<string> DiscoverArchiveOrgCandidateUrls(BiosFile entry, string s3AuthHeader)
        {
            var discovered = new List<string>();
            if (_httpClient == null || string.IsNullOrWhiteSpace(entry?.FileName))
            {
                return discovered;
            }

            try
            {
                var searchQuery = $"\"{entry.FileName}\"";
                var searchUrl = $"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(searchQuery)}&fl[]=identifier&rows=3&output=json";
                var searchReq = new HttpRequest(searchUrl, HttpAccept.Json)
                {
                    SuppressHttpError = true,
                    RequestTimeout = TimeSpan.FromSeconds(12)
                };

                var searchResp = _httpClient.Get(searchReq);
                if (searchResp == null || searchResp.HasHttpError || string.IsNullOrWhiteSpace(searchResp.Content))
                {
                    return discovered;
                }

                var searchJson = Newtonsoft.Json.Linq.JObject.Parse(searchResp.Content);
                var docs = searchJson["response"]?["docs"] as Newtonsoft.Json.Linq.JArray;
                if (docs == null)
                {
                    return discovered;
                }

                var baseName = Path.GetFileNameWithoutExtension(entry.FileName);
                foreach (var doc in docs)
                {
                    var id = doc["identifier"]?.ToString();
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var metaUrl = $"https://archive.org/metadata/{Uri.EscapeDataString(id)}/files";
                    var metaReq = new HttpRequest(metaUrl, HttpAccept.Json)
                    {
                        SuppressHttpError = true,
                        RequestTimeout = TimeSpan.FromSeconds(12)
                    };

                    if (!string.IsNullOrWhiteSpace(s3AuthHeader))
                    {
                        metaReq.Headers["Authorization"] = s3AuthHeader;
                    }

                    var metaResp = _httpClient.Get(metaReq);
                    if (metaResp == null || metaResp.HasHttpError || string.IsNullOrWhiteSpace(metaResp.Content))
                    {
                        continue;
                    }

                    var metaJson = Newtonsoft.Json.Linq.JObject.Parse(metaResp.Content);
                    var files = metaJson["result"] as Newtonsoft.Json.Linq.JArray;
                    if (files == null)
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        var fileName = file["name"]?.ToString();
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            continue;
                        }

                        var leaf = Path.GetFileName(fileName);
                        if (string.Equals(leaf, entry.FileName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, $"{entry.FileName}.zip", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, $"{baseName}.zip", StringComparison.OrdinalIgnoreCase))
                        {
                            var escapedPath = string.Join("/", fileName.TrimStart('/').Split('/').Select(Uri.EscapeDataString));
                            discovered.Add($"https://archive.org/download/{Uri.EscapeDataString(id)}/{escapedPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Dynamic Archive.org BIOS discovery failed for {0}", entry.FileName);
            }

            return discovered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

                var grabbedHistory = new GameHistory
                {
                    EventType = GameHistoryEventType.Grabbed,
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

                var importedHistory = new GameHistory
                {
                    EventType = GameHistoryEventType.DownloadFolderImported,
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
                        if (!valid && IsCompressedArchive(foundPath) && !entry.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            var targetRawPath = Path.Combine(Path.GetDirectoryName(foundPath) ?? biosDir, entry.FileName);
                            valid = TryExtractBiosFromArchive(foundPath, targetRawPath, entry);
                            if (valid)
                            {
                                entry.ResolvedPath = FormatDisplayPath(targetRawPath);
                            }
                        }

                        if (valid)
                        {
                            entry.IsVerified = true;
                            entry.Status = 1;
                        }
                        else
                        {
                            _logger.Warn("BIOS file {0} at {1} failed hash verification. Discarding invalid file and replacing with verified copy.", entry.FileName, foundPath);
                            try
                            {
                                File.Delete(foundPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, "Failed to discard invalid BIOS file at {0}", foundPath);
                            }

                            var targetDir = Path.GetDirectoryName(foundPath) ?? biosDir;
                            var replaced = TryDownloadAndVerifyBios(entry, targetDir, requestedPlatformSlug);
                            if (replaced)
                            {
                                entry.IsVerified = true;
                                entry.Status = 1;
                            }
                            else
                            {
                                entry.IsVerified = false;
                                entry.Status = 2;
                            }
                        }
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
