using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Discord;
using NzbDrone.Core.Notifications.Discord.Payloads;
using NzbDrone.Core.Notifications.Mailgun;
using NzbDrone.Core.Notifications.Pushover;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class NotificationServiceFixture : CoreTest<NotificationService>
    {
        private Series _platform;
        private Episode _game;

        [SetUp]
        public void Setup()
        {
            _platform = new Series
            {
                Id = 1,
                Title = "Super Nintendo Entertainment System",
                Path = @"C:\Roms\SNES",
                Ratings = new Ratings { Value = 9.0m },
                Genres = new List<string> { "Platformer" },
                Tags = new HashSet<int>()
            };

            _game = new Episode
            {
                Id = 10,
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Super Mario World",
                Overview = "Classic platformer"
            };
        }

        [Test]
        public void should_format_grab_import_delete_and_collection_notifications_with_gaming_vocabulary()
        {
            var notification = new Mock<INotification>();
            var definition = new NotificationDefinition
            {
                Id = 1,
                Name = "TestNotification",
                OnGrab = true,
                OnDownload = true,
                OnImportComplete = true,
                OnEpisodeFileDelete = true,
                OnManualInteractionRequired = true,
                Tags = new HashSet<int>()
            };
            notification.SetupGet(n => n.Definition).Returns(definition);

            var providers = new List<INotification> { notification.Object };
            Mocker.GetMock<INotificationFactory>().Setup(f => f.OnGrabEnabled(true)).Returns(providers);
            Mocker.GetMock<INotificationFactory>().Setup(f => f.OnDownloadEnabled(true)).Returns(providers);
            Mocker.GetMock<INotificationFactory>().Setup(f => f.OnImportCompleteEnabled(true)).Returns(providers);
            Mocker.GetMock<INotificationFactory>().Setup(f => f.OnEpisodeFileDeleteEnabled(true)).Returns(providers);
            Mocker.GetMock<INotificationFactory>().Setup(f => f.OnManualInteractionEnabled(true)).Returns(providers);

            GrabMessage capturedGrab = null;
            DownloadMessage capturedDownload = null;
            ImportCompleteMessage capturedImportComplete = null;
            GameDeleteMessage capturedDelete = null;
            ManualInteractionRequiredMessage capturedManual = null;

            notification.Setup(n => n.OnGrab(It.IsAny<GrabMessage>())).Callback<GrabMessage>(m => capturedGrab = m);
            notification.Setup(n => n.OnDownload(It.IsAny<DownloadMessage>())).Callback<DownloadMessage>(m => capturedDownload = m);
            notification.Setup(n => n.OnImportComplete(It.IsAny<ImportCompleteMessage>())).Callback<ImportCompleteMessage>(m => capturedImportComplete = m);
            notification.Setup(n => n.OnEpisodeFileDelete(It.IsAny<GameDeleteMessage>())).Callback<GameDeleteMessage>(m => capturedDelete = m);
            notification.Setup(n => n.OnManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>())).Callback<ManualInteractionRequiredMessage>(m => capturedManual = m);

            var quality = new QualityModel(Quality.NoIntroVerified, new Revision(version: 2));
            var remoteGame = new RemoteEpisode
            {
                Series = _platform,
                Episodes = new List<Episode> { _game },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = quality, ReleaseGroup = "No-Intro" },
                Release = new ReleaseInfo { Title = "Super Mario World (USA)" }
            };

            Subject.Handle(new GameGrabbedEvent(remoteGame));

            var romFile = new EpisodeFile
            {
                Id = 100,
                Series = _platform,
                Episodes = new List<Episode> { _game },
                SeasonNumber = 1,
                RelativePath = "USA/Super Mario World (USA).sfc",
                Quality = quality
            };
            var localGame = new LocalEpisode
            {
                Series = _platform,
                Episodes = new List<Episode> { _game },
                Quality = quality,
                Path = @"C:\Downloads\Super Mario World (USA).sfc"
            };

            Subject.Handle(new GameImportedEvent(localGame, romFile, new List<DeletedRomFile>(), true, null));

            var fullRegionRemote = new RemoteEpisode
            {
                Series = _platform,
                Episodes = new List<Episode> { _game },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = quality, FullSeason = true }
            };
            var trackedDownload = new TrackedDownload
            {
                RemoteEpisode = fullRegionRemote,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "dl-1",
                    Title = "SNES USA Set",
                    OutputPath = new NzbDrone.Common.Disk.OsPath(@"C:\Downloads\SNES_USA")
                }
            };

            Subject.Handle(new DownloadCompletedEvent(trackedDownload, _platform.Id, new List<EpisodeFile> { romFile }, null));
            Subject.Handle(new RomFileDeletedEvent(romFile, DeleteMediaFileReason.Manual));
            Subject.Handle(new ManualInteractionRequiredEvent(trackedDownload, null));

            var singleExpected = "Super Nintendo Entertainment System (USA) - Super Mario World [NoIntroVerified Rev 2]";
            var collectionExpected = "Super Nintendo Entertainment System - USA Collection [NoIntroVerified Rev 2]";

            capturedGrab.Message.Should().Be(singleExpected);
            capturedDownload.Message.Should().Be(singleExpected);
            capturedDelete.Message.Should().Be(singleExpected);
            capturedManual.Message.Should().Be(singleExpected);
            capturedImportComplete.Message.Should().Be(collectionExpected);

            foreach (var msg in new[] { capturedGrab.Message, capturedDownload.Message, capturedImportComplete.Message, capturedDelete.Message, capturedManual.Message })
            {
                msg.Should().NotContain("Season");
                msg.Should().NotContain("1x01");
                msg.Should().NotContain("Episode");
                msg.Should().NotContain("Proper");
                msg.Should().NotContain("Sonarr");
            }
        }

        [Test]
        public void pushover_should_use_controllarr_branding_and_gaming_titles()
        {
            var httpClient = Mocker.GetMock<IHttpClient>();
            HttpRequest capturedTestRequest = null;
            httpClient.Setup(c => c.Post(It.IsAny<HttpRequest>()))
                      .Callback<HttpRequest>(r => capturedTestRequest = r)
                      .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), string.Empty));

            var pushoverProxy = new PushoverProxy(httpClient.Object, Mocker.GetMock<ILocalizationService>().Object, LogManager.GetCurrentClassLogger());
            var settings = new PushoverSettings { ApiKey = "app-token", UserKey = "user-key" };

            var failure = pushoverProxy.Test(settings);
            failure.Should().BeNull();
            var formBody = Encoding.UTF8.GetString(capturedTestRequest.ContentData);
            formBody.Should().Contain("Controllarr%20-%20Test%20Notification");
            formBody.Should().Contain("This%20is%20a%20test%20message%20from%20Controllarr");
            formBody.Should().NotContain("Sonarr");

            var proxyMock = new Mock<IPushoverProxy>();
            var pushover = new Pushover(proxyMock.Object)
            {
                Definition = new NotificationDefinition { Settings = settings }
            };

            pushover.OnGrab(new GrabMessage { Message = "SNES (USA) - Super Mario World" });
            pushover.OnDownload(new DownloadMessage { Message = "SNES (USA) - Super Mario World" });

            proxyMock.Verify(p => p.SendNotification("Controllarr - Game Grabbed", "SNES (USA) - Super Mario World", settings), Times.Once());
            proxyMock.Verify(p => p.SendNotification("Controllarr - Game Downloaded", "SNES (USA) - Super Mario World", settings), Times.Once());
        }

        [Test]
        public void mailgun_should_use_controllarr_test_message()
        {
            var mailgunProxy = Mocker.GetMock<IMailgunProxy>();
            var settings = new MailgunSettings { ApiKey = "key", From = "from@example.com", SenderDomain = "example.com" };
            var mailgun = new MailGun(mailgunProxy.Object, Mocker.GetMock<ILocalizationService>().Object, LogManager.GetCurrentClassLogger())
            {
                Definition = new NotificationDefinition { Settings = settings }
            };

            var result = mailgun.Test();

            result.IsValid.Should().BeTrue();
            mailgunProxy.Verify(p => p.SendNotification("Controllarr - Test Notification", "This is a test message from Controllarr, through Mailgun.", settings), Times.Once());
        }

        [Test]
        public void discord_should_use_controllarr_icon_gaming_descriptions_and_omit_tv_fields()
        {
            var discordProxy = Mocker.GetMock<IDiscordProxy>();
            var configFileProvider = Mocker.GetMock<IConfigFileProvider>();
            configFileProvider.SetupGet(c => c.InstanceName).Returns("Controllarr");

            DiscordPayload grabPayload = null;
            DiscordPayload importPayload = null;
            discordProxy.Setup(p => p.SendPayload(It.IsAny<DiscordPayload>(), It.IsAny<DiscordSettings>()))
                        .Callback<DiscordPayload, DiscordSettings>((p, _) =>
                        {
                            if (p.Embeds?.FirstOrDefault()?.Description == "Game Grabbed")
                            {
                                grabPayload = p;
                            }
                            else
                            {
                                importPayload = p;
                            }
                        });

            var settings = new DiscordSettings();
            var discord = new Discord(discordProxy.Object, configFileProvider.Object, Mocker.GetMock<ILocalizationService>().Object)
            {
                Definition = new NotificationDefinition { Settings = settings }
            };

            discord.OnGrab(new GrabMessage
            {
                Series = _platform,
                Quality = new QualityModel(Quality.NoIntroVerified),
                Episode = new RemoteEpisode
                {
                    Series = _platform,
                    Episodes = new List<Episode> { _game },
                    ParsedEpisodeInfo = new ParsedEpisodeInfo { ReleaseGroup = "No-Intro" },
                    Release = new ReleaseInfo { Title = "Super Mario World (USA)", Size = 524288, Indexer = "Archive" }
                }
            });

            var romFile = new EpisodeFile
            {
                Series = _platform,
                Episodes = new List<Episode> { _game },
                Quality = new QualityModel(Quality.NoIntroVerified),
                Size = 524288,
                Languages = new List<Language> { Language.English },
                SceneName = "Super Mario World (USA)"
            };

            discord.OnDownload(new DownloadMessage
            {
                Series = _platform,
                EpisodeFile = romFile,
                EpisodeInfo = new LocalEpisode(),
                OldFiles = new List<DeletedRomFile>()
            });

            grabPayload.Should().NotBeNull();
            var grabEmbed = grabPayload.Embeds.Single();
            grabEmbed.Description.Should().Be("Game Grabbed");
            grabEmbed.Title.Should().Be("Super Nintendo Entertainment System (USA) - Super Mario World");
            grabEmbed.Author.IconUrl.Should().Be("https://raw.githubusercontent.com/12thirtyfour/Controllarr/main/Logo/256.png");
            grabEmbed.Fields.First(f => f.Name == "Links").Value.Should().NotContain("IMDB");

            importPayload.Should().NotBeNull();
            var importEmbed = importPayload.Embeds.Single();
            importEmbed.Description.Should().Be("Game Imported");
            importEmbed.Title.Should().Be("Super Nintendo Entertainment System (USA) - Super Mario World");
            importEmbed.Author.IconUrl.Should().Be("https://raw.githubusercontent.com/12thirtyfour/Controllarr/main/Logo/256.png");
            importEmbed.Fields.Select(f => f.Name).Should().NotContain("Codecs");
            importEmbed.Fields.Select(f => f.Name).Should().NotContain("Subtitles");
        }
    }
}
