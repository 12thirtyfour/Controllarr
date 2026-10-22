sed -i 's/GameMetadataProxyStub/IgdbMetadataProxy/g' src/NzbDrone.Core.Test/MetadataSource/GameMetadataFixture.cs
sed -i '/using NzbDrone.Core.MetadataSource.Game.Model;/a using NzbDrone.Core.MetadataSource.Igdb;' src/NzbDrone.Core.Test/MetadataSource/GameMetadataFixture.cs
