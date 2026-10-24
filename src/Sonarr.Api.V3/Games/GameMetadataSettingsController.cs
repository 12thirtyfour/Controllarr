using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Dat;
using NzbDrone.Core.MetadataSource.Game;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("gamemetadata")]
    [V5ApiController("gamemetadata")]
    public class GameMetadataSettingsController : Controller
    {
        private readonly IGameMediaAssetService _gameMediaAssetService;

        public GameMetadataSettingsController(IGameMediaAssetService gameMediaAssetService)
        {
            _gameMediaAssetService = gameMediaAssetService;
        }

        [HttpGet("settings")]
        [Produces("application/json")]
        public ActionResult<UnifiedMetadataSettingsModel> GetUnifiedSettings()
        {
            return Ok(_gameMediaAssetService.GetUnifiedSettings());
        }

        [HttpPost("settings")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<UnifiedMetadataSettingsModel> SaveUnifiedSettings([FromBody] UnifiedMetadataSettingsModel request)
        {
            return Ok(_gameMediaAssetService.SaveUnifiedSettings(request));
        }

        [HttpGet("sources")]
        [Produces("application/json")]
        public ActionResult<MetadataSourceConfigModel> GetSources()
        {
            return Ok(_gameMediaAssetService.GetMetadataSources());
        }

        [HttpPost("sources")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<MetadataSourceConfigModel> SaveSources([FromBody] MetadataSourceConfigModel request)
        {
            return Ok(_gameMediaAssetService.SaveMetadataSources(request));
        }

        [HttpGet("assets")]
        [Produces("application/json")]
        public ActionResult<GameMediaAssetSettingsModel> GetAssets()
        {
            return Ok(_gameMediaAssetService.GetMediaAssetSettings());
        }

        [HttpPost("assets")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<GameMediaAssetSettingsModel> SaveAssets([FromBody] GameMediaAssetSettingsModel request)
        {
            return Ok(_gameMediaAssetService.SaveMediaAssetSettings(request));
        }

        [HttpGet("dats")]
        [Produces("application/json")]
        public ActionResult<List<DatFile>> GetDats()
        {
            return Ok(_gameMediaAssetService.GetDatFiles());
        }

        [HttpPost("dats/import")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<DatImportResultModel> ImportDat([FromBody] DatImportRequestModel request)
        {
            return Ok(_gameMediaAssetService.ImportDatOrSmdb(request));
        }
    }
}
