using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using Sonarr.Http.Extensions;

namespace Sonarr.Http.Frontend.Mappers
{
    public class LoginHtmlMapper : HtmlMapperBase
    {
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IConfigFileProvider _configFileProvider;

        public LoginHtmlMapper(IAppFolderInfo appFolderInfo,
                               IDiskProvider diskProvider,
                               Lazy<ICacheBreakerProvider> cacheBreakProviderFactory,
                               IConfigFileProvider configFileProvider,
                               Logger logger)
            : base(diskProvider, configFileProvider, cacheBreakProviderFactory, logger)
        {
            _appFolderInfo = appFolderInfo;
            _configFileProvider = configFileProvider;
        }

        protected override string FolderPath => _appFolderInfo.GetUiFolder();
        protected override string HtmlPath => Path.Combine(FolderPath, "login.html");

        protected override string MapPath(string resourceUrl)
        {
            return HtmlPath;
        }

        public override bool CanHandle(string resourceUrl)
        {
            return resourceUrl.StartsWith("/login");
        }

        public override Task<IActionResult> GetResponse(HttpContext context, string resourceUrl)
        {
            var isLocal = IPAddress.TryParse(context.GetRemoteIP(), out var ipAddress) &&
                          (ipAddress.IsLocalAddress() || IPAddress.IsLoopback(ipAddress));

            if (_configFileProvider.AuthenticationMethod == AuthenticationType.None ||
                (_configFileProvider.AuthenticationRequired == AuthenticationRequiredType.DisabledForLocalAddresses && isLocal) ||
                (_configFileProvider.AuthenticationRequired == AuthenticationRequiredType.DisabledForLocalhost && ipAddress != null && IPAddress.IsLoopback(ipAddress)))
            {
                var returnUrl = context.Request.Query["returnUrl"].ToString();
                var destination = string.IsNullOrWhiteSpace(returnUrl) ? _configFileProvider.UrlBase + "/" : returnUrl;
                return Task.FromResult<IActionResult>(new RedirectResult(destination));
            }

            return base.GetResponse(context, resourceUrl);
        }

        protected override string GetHtmlText(HttpContext context)
        {
            var html = base.GetHtmlText(context);
            var theme = _configFileProvider.Theme;
            var authMethod = _configFileProvider.AuthenticationMethod.ToString().ToLowerInvariant();

            if (_configFileProvider.AuthenticationMethod == AuthenticationType.Oidc &&
                !_configFileProvider.IsOidcConfigured())
            {
                authMethod += " oidc-misconfigured";
            }

            html = html.Replace("_THEME_", theme);
            html = html.Replace("_AUTH_METHOD_", authMethod);

            return html;
        }
    }
}
