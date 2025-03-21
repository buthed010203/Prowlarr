using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers.Definitions.Cardigann.Exceptions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers.Definitions.Cardigann
{
    public class CardigannRequestGenerator : CardigannBase, IIndexerRequestGenerator
    {
        public IIndexerHttpClient HttpClient { get; set; }
        public ProviderDefinition Definition { get; set; }
        public IDictionary<string, string> Cookies { get; set; }
        protected HttpResponse landingResult;
        protected IHtmlDocument landingResultDocument;
        protected override string SiteLink => ResolveSiteLink();

        private readonly TimeSpan _rateLimit;

        public CardigannRequestGenerator(IConfigService configService,
                                         CardigannDefinition definition,
                                         Logger logger,
                                         TimeSpan rateLimit)
        : base(configService, definition, logger)
        {
            _rateLimit = rateLimit;
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
        {
            _logger.Trace("Getting Movie search");

            var pageableRequests = new IndexerPageableRequestChain();

            var variables = GetQueryVariableDefaults(searchCriteria);

            variables[".Query.Movie"] = null;
            variables[".Query.Year"] = searchCriteria.Year?.ToString() ?? null;
            variables[".Query.Genre"] = searchCriteria.Genre;
            variables[".Query.IMDBID"] = searchCriteria.FullImdbId;
            variables[".Query.IMDBIDShort"] = searchCriteria.ImdbId;
            variables[".Query.TMDBID"] = searchCriteria.TmdbId?.ToString() ?? null;
            variables[".Query.TraktID"] = searchCriteria.TraktId?.ToString() ?? null;
            variables[".Query.DoubanID"] = searchCriteria.DoubanId?.ToString() ?? null;

            pageableRequests.Add(GetRequest(variables, searchCriteria));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            _logger.Trace("Getting Music search");

            var pageableRequests = new IndexerPageableRequestChain();

            var variables = GetQueryVariableDefaults(searchCriteria);

            variables[".Query.Album"] = searchCriteria.Album;
            variables[".Query.Artist"] = searchCriteria.Artist;
            variables[".Query.Label"] = searchCriteria.Label;
            variables[".Query.Genre"] = searchCriteria.Genre;
            variables[".Query.Year"] = searchCriteria.Year?.ToString() ?? null;
            variables[".Query.Track"] = searchCriteria.Track;

            pageableRequests.Add(GetRequest(variables, searchCriteria));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(TvSearchCriteria searchCriteria)
        {
            _logger.Trace("Getting TV search");

            var pageableRequests = new IndexerPageableRequestChain();

            var variables = GetQueryVariableDefaults(searchCriteria);

            variables[".Query.Series"] = null;
            variables[".Query.Ep"] = searchCriteria.Episode;
            variables[".Query.Season"] = searchCriteria.Season?.ToString() ?? null;
            variables[".Query.Genre"] = searchCriteria.Genre;
            variables[".Query.Year"] = searchCriteria.Year?.ToString() ?? null;
            variables[".Query.IMDBID"] = searchCriteria.FullImdbId;
            variables[".Query.IMDBIDShort"] = searchCriteria.ImdbId;
            variables[".Query.TVDBID"] = searchCriteria.TvdbId?.ToString() ?? null;
            variables[".Query.TMDBID"] = searchCriteria.TmdbId?.ToString() ?? null;
            variables[".Query.TVRageID"] = searchCriteria.RId?.ToString() ?? null;
            variables[".Query.TVMazeID"] = searchCriteria.TvMazeId?.ToString() ?? null;
            variables[".Query.TraktID"] = searchCriteria.TraktId?.ToString() ?? null;
            variables[".Query.DoubanID"] = searchCriteria.DoubanId?.ToString() ?? null;
            variables[".Query.Episode"] = searchCriteria.EpisodeSearchString;

            pageableRequests.Add(GetRequest(variables, searchCriteria));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            _logger.Trace("Getting Book search");

            var pageableRequests = new IndexerPageableRequestChain();

            var variables = GetQueryVariableDefaults(searchCriteria);

            variables[".Query.Author"] = searchCriteria.Author;
            variables[".Query.Title"] = searchCriteria.Title;
            variables[".Query.Genre"] = searchCriteria.Genre;
            variables[".Query.Publisher"] = searchCriteria.Publisher;
            variables[".Query.Year"] = searchCriteria.Year?.ToString() ?? null;

            pageableRequests.Add(GetRequest(variables, searchCriteria));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            _logger.Trace("Getting Basic search");

            var pageableRequests = new IndexerPageableRequestChain();

            var variables = GetQueryVariableDefaults(searchCriteria);

            pageableRequests.Add(GetRequest(variables, searchCriteria));

            return pageableRequests;
        }

        private Dictionary<string, object> GetQueryVariableDefaults(SearchCriteriaBase searchCriteria)
        {
            var variables = GetBaseTemplateVariables();

            variables[".Query.Type"] = searchCriteria.SearchType;
            variables[".Query.Q"] = searchCriteria.SearchTerm;
            variables[".Query.Categories"] = searchCriteria.Categories;
            variables[".Query.Limit"] = searchCriteria.Limit?.ToString() ?? null;
            variables[".Query.Offset"] = searchCriteria.Offset?.ToString() ?? null;
            variables[".Query.Extended"] = null;
            variables[".Query.APIKey"] = null;
            variables[".Query.Genre"] = null;

            //Movie
            variables[".Query.Movie"] = null;
            variables[".Query.Year"] = null;
            variables[".Query.IMDBID"] = null;
            variables[".Query.IMDBIDShort"] = null;
            variables[".Query.TMDBID"] = null;

            //Tv
            variables[".Query.Series"] = null;
            variables[".Query.Ep"] = null;
            variables[".Query.Season"] = null;
            variables[".Query.TVDBID"] = null;
            variables[".Query.TVRageID"] = null;
            variables[".Query.TVMazeID"] = null;
            variables[".Query.TraktID"] = null;
            variables[".Query.DoubanID"] = null;
            variables[".Query.Episode"] = null;

            //Music
            variables[".Query.Album"] = null;
            variables[".Query.Artist"] = null;
            variables[".Query.Label"] = null;
            variables[".Query.Track"] = null;

            //Book
            variables[".Query.Author"] = null;
            variables[".Query.Title"] = null;
            variables[".Query.Publisher"] = null;

            return variables;
        }

        public async Task DoLogin()
        {
            var login = _definition.Login;

            var variables = GetBaseTemplateVariables();
            var headers = ParseCustomHeaders(_definition.Login?.Headers ?? _definition.Search?.Headers, variables);

            if (login.Method == "post")
            {
                var pairs = new Dictionary<string, string>();

                foreach (var input in login.Inputs)
                {
                    var value = ApplyGoTemplateText(input.Value);
                    pairs.Add(input.Key, value);
                }

                var loginUrl = ResolvePath(login.Path).ToString();

                CookiesUpdater(null, null);

                var requestBuilder = new HttpRequestBuilder(loginUrl)
                {
                    LogResponseContent = true,
                    Method = HttpMethod.Post,
                    AllowAutoRedirect = true,
                    SuppressHttpError = true,
                    Encoding = _encoding
                };

                foreach (var pair in pairs)
                {
                    requestBuilder.AddFormParameter(pair.Key, pair.Value);
                }

                Cookies = null;
                if (login.Cookies != null)
                {
                    Cookies = CookieUtil.CookieHeaderToDictionary(string.Join("; ", login.Cookies));
                }

                var request = requestBuilder
                    .SetCookies(Cookies ?? new Dictionary<string, string>())
                    .SetHeaders(headers ?? new Dictionary<string, string>())
                    .SetHeader("Referer", SiteLink)
                    .WithRateLimit(_rateLimit.TotalSeconds)
                    .Build();

                var response = await HttpClient.ExecuteProxiedAsync(request, Definition);

                Cookies = response.GetCookies();

                CheckForError(response, login.Error);

                CookiesUpdater(Cookies, DateTime.Now.AddDays(30));
            }
            else if (login.Method == "form")
            {
                var loginUrl = ResolvePath(login.Path).ToString();

                var queryCollection = new NameValueCollection();
                var pairs = new Dictionary<string, string>();

                var formSelector = login.Form ?? "form";

                // landingResultDocument might not be initiated if the login is caused by a re-login during a query
                if (landingResultDocument == null)
                {
                    await GetConfigurationForSetup(true);
                }

                var form = landingResultDocument.QuerySelector(formSelector);
                if (form == null)
                {
                    throw new CardigannConfigException(_definition, string.Format("Login failed: No form found on {0} using form selector {1}", loginUrl, formSelector));
                }

                var inputs = form.QuerySelectorAll("input");
                if (inputs == null)
                {
                    throw new CardigannConfigException(_definition, string.Format("Login failed: No inputs found on {0} using form selector {1}", loginUrl, formSelector));
                }

                var submitUrlstr = form.GetAttribute("action");
                if (login.Submitpath != null)
                {
                    submitUrlstr = login.Submitpath;
                }

                foreach (var input in inputs)
                {
                    var name = input.GetAttribute("name");
                    if (name == null)
                    {
                        continue;
                    }

                    var value = input.GetAttribute("value") ?? "";

                    pairs[name] = value;
                }

                foreach (var input in login.Inputs)
                {
                    var value = ApplyGoTemplateText(input.Value);
                    var inputKey = input.Key;
                    if (login.Selectors)
                    {
                        var inputElement = landingResultDocument.QuerySelector(input.Key);
                        if (inputElement == null)
                        {
                            throw new CardigannConfigException(_definition, string.Format("Login failed: No input found using selector {0}", input.Key));
                        }

                        inputKey = inputElement.GetAttribute("name");
                    }

                    pairs[inputKey] = value;
                }

                // selector inputs
                if (login.Selectorinputs != null)
                {
                    foreach (var selectorinput in login.Selectorinputs)
                    {
                        string value = null;
                        try
                        {
                            value = HandleSelector(selectorinput.Value, landingResultDocument.FirstElementChild);
                            pairs[selectorinput.Key] = value;
                        }
                        catch (Exception ex)
                        {
                            throw new CardigannException(string.Format("Error while parsing selector input={0}, selector={1}, value={2}: {3}", selectorinput.Key, selectorinput.Value.Selector, value, ex.Message));
                        }
                    }
                }

                // getselector inputs
                if (login.Getselectorinputs != null)
                {
                    foreach (var selectorinput in login.Getselectorinputs)
                    {
                        string value = null;
                        try
                        {
                            value = HandleSelector(selectorinput.Value, landingResultDocument.FirstElementChild);
                            queryCollection[selectorinput.Key] = value;
                        }
                        catch (Exception ex)
                        {
                            throw new CardigannException(string.Format("Error while parsing get selector input={0}, selector={1}, value={2}: {3}", selectorinput.Key, selectorinput.Value.Selector, value, ex.Message));
                        }
                    }
                }

                if (queryCollection.Count > 0)
                {
                    submitUrlstr += "?" + queryCollection.GetQueryString();
                }

                var submitUrl = ResolvePath(submitUrlstr, new Uri(loginUrl));

                // automatically solve simpleCaptchas, if used
                var simpleCaptchaPresent = landingResultDocument.QuerySelector("script[src*=\"simpleCaptcha\"]");
                if (simpleCaptchaPresent != null)
                {
                    var captchaUrl = ResolvePath("simpleCaptcha.php?numImages=1");

                    var requestBuilder = new HttpRequestBuilder(captchaUrl.ToString())
                    {
                        LogResponseContent = true,
                        Method = HttpMethod.Get,
                        Encoding = _encoding
                    };

                    var request = requestBuilder
                        .SetCookies(Cookies)
                        .SetHeaders(headers ?? new Dictionary<string, string>())
                        .SetHeader("Referer", loginUrl)
                        .WithRateLimit(_rateLimit.TotalSeconds)
                        .Build();

                    var simpleCaptchaResult = await HttpClient.ExecuteProxiedAsync(request, Definition);

                    var simpleCaptchaJSON = JObject.Parse(simpleCaptchaResult.Content);
                    var captchaSelection = simpleCaptchaJSON["images"][0]["hash"].ToString();
                    pairs["captchaSelection"] = captchaSelection;
                    pairs["submitme"] = "X";
                }

                if (login.Captcha != null)
                {
                    var captcha = login.Captcha;
                    Settings.ExtraFieldData.TryGetValue("CAPTCHA", out var captchaText);
                    if (captchaText != null)
                    {
                        var input = captcha.Input;
                        if (login.Selectors)
                        {
                            var inputElement = landingResultDocument.QuerySelector(captcha.Input);
                            if (inputElement == null)
                            {
                                throw new CardigannConfigException(_definition, string.Format("Login failed: No captcha input found using {0}", captcha.Input));
                            }

                            input = inputElement.GetAttribute("name");
                        }

                        pairs[input] = (string)captchaText;
                    }
                }

                // clear landingResults/Document, otherwise we might use an old version for a new relogin (if GetConfigurationForSetup() wasn't called before)
                landingResult = null;
                landingResultDocument = null;

                HttpResponse loginResult = null;
                var enctype = form.GetAttribute("enctype");
                if (enctype == "multipart/form-data")
                {
                    var boundary = "---------------------------" + DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds.ToString().Replace(".", "");
                    var bodyParts = new List<string>();

                    foreach (var pair in pairs)
                    {
                        var part = "--" + boundary + "\r\n" +
                          "Content-Disposition: form-data; name=\"" + pair.Key + "\"\r\n" +
                          "\r\n" +
                          pair.Value;
                        bodyParts.Add(part);
                    }

                    bodyParts.Add("--" + boundary + "--");

                    headers.Add("Content-Type", "multipart/form-data; boundary=" + boundary);
                    var body = string.Join("\r\n", bodyParts);

                    var requestBuilder = new HttpRequestBuilder(submitUrl.ToString())
                    {
                        LogResponseContent = true,
                        Method = HttpMethod.Post,
                        AllowAutoRedirect = true,
                        Encoding = _encoding
                    };

                    foreach (var pair in pairs)
                    {
                        requestBuilder.AddFormParameter(pair.Key, pair.Value);
                    }

                    var request = requestBuilder
                        .SetCookies(Cookies)
                        .SetHeaders(headers ?? new Dictionary<string, string>())
                        .SetHeader("Referer", SiteLink)
                        .WithRateLimit(_rateLimit.TotalSeconds)
                        .Build();

                    request.SetContent(body);

                    loginResult = await HttpClient.ExecuteProxiedAsync(request, Definition);
                }
                else
                {
                    var requestBuilder = new HttpRequestBuilder(submitUrl.ToString())
                    {
                        LogResponseContent = true,
                        Method = HttpMethod.Post,
                        AllowAutoRedirect = true,
                        SuppressHttpError = true,
                        Encoding = _encoding
                    };

                    foreach (var pair in pairs)
                    {
                        requestBuilder.AddFormParameter(pair.Key, pair.Value);
                    }

                    var request = requestBuilder
                        .SetCookies(Cookies)
                        .SetHeaders(headers ?? new Dictionary<string, string>())
                        .SetHeader("Referer", loginUrl)
                        .WithRateLimit(_rateLimit.TotalSeconds)
                        .Build();

                    loginResult = await HttpClient.ExecuteProxiedAsync(request, Definition);
                }

                Cookies = loginResult.GetCookies();
                CheckForError(loginResult, login.Error);
                CookiesUpdater(Cookies, DateTime.Now.AddDays(30));
            }
            else if (login.Method == "cookie")
            {
                CookiesUpdater(null, null);
                Settings.ExtraFieldData.TryGetValue("cookie", out var cookies);
                CookiesUpdater(CookieUtil.CookieHeaderToDictionary((string)cookies), DateTime.Now.AddDays(30));
            }
            else if (login.Method == "get")
            {
                var queryCollection = new NameValueCollection();
                foreach (var input in login.Inputs)
                {
                    var value = ApplyGoTemplateText(input.Value);
                    queryCollection.Add(input.Key, value);
                }

                var loginUrl = ResolvePath(login.Path + "?" + queryCollection.GetQueryString()).ToString();

                CookiesUpdater(null, null);

                var requestBuilder = new HttpRequestBuilder(loginUrl)
                {
                    LogResponseContent = true,
                    Method = HttpMethod.Get,
                    SuppressHttpError = true,
                    Encoding = _encoding
                };

                var request = requestBuilder
                    .SetHeaders(headers ?? new Dictionary<string, string>())
                    .SetHeader("Referer", SiteLink)
                    .WithRateLimit(_rateLimit.TotalSeconds)
                    .Build();

                var response = await HttpClient.ExecuteProxiedAsync(request, Definition);

                Cookies = response.GetCookies();

                CheckForError(response, login.Error);

                CookiesUpdater(Cookies, DateTime.Now.AddDays(30));
            }
            else if (login.Method == "oneurl")
            {
                var oneUrl = ApplyGoTemplateText(login.Inputs["oneurl"]);
                var loginUrl = ResolvePath(login.Path + oneUrl).ToString();

                CookiesUpdater(null, null);

                var requestBuilder = new HttpRequestBuilder(loginUrl)
                {
                    LogResponseContent = true,
                    Method = HttpMethod.Get,
                    SuppressHttpError = true,
                    Encoding = _encoding
                };

                var request = requestBuilder
                    .SetHeaders(headers ?? new Dictionary<string, string>())
                    .SetHeader("Referer", SiteLink)
                    .WithRateLimit(_rateLimit.TotalSeconds)
                    .Build();

                var response = await HttpClient.ExecuteProxiedAsync(request, Definition);

                Cookies = response.GetCookies();

                CheckForError(response, login.Error);

                CookiesUpdater(Cookies, DateTime.Now.AddDays(30));
            }
            else
            {
                throw new NotImplementedException($"Login method {login.Method} not implemented");
            }
        }

        protected bool CheckForError(HttpResponse loginResult, IList<ErrorBlock> errorBlocks)
        {
            if (loginResult.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpException(loginResult);
            }

            if (errorBlocks == null)
            {
                return true;
            }

            var resultParser = new HtmlParser();
            var resultDocument = resultParser.ParseDocument(loginResult.Content);
            foreach (var error in errorBlocks)
            {
                var selection = resultDocument.QuerySelector(error.Selector);
                if (selection != null)
                {
                    var errorMessage = selection.TextContent;
                    if (error.Message != null)
                    {
                        errorMessage = HandleSelector(error.Message, resultDocument.FirstElementChild);
                    }

                    throw new CardigannConfigException(_definition, string.Format("Error: {0}", errorMessage.Trim()));
                }
            }

            return true;
        }

        public async Task<Captcha> GetConfigurationForSetup(bool automaticLogin)
        {
            var login = _definition.Login;

            if (login == null || login.Method != "form")
            {
                return null;
            }

            var variables = GetBaseTemplateVariables();
            var headers = ParseCustomHeaders(_definition.Login?.Headers ?? _definition.Search?.Headers, variables);

            var loginUrl = ResolvePath(login.Path);

            var requestBuilder = new HttpRequestBuilder(loginUrl.AbsoluteUri)
            {
                LogResponseContent = true,
                Method = HttpMethod.Get,
                Encoding = _encoding
            };

            Cookies = null;
            if (login.Cookies != null)
            {
                Cookies = CookieUtil.CookieHeaderToDictionary(string.Join("; ", login.Cookies));
            }

            var request = requestBuilder
                .SetCookies(Cookies ?? new Dictionary<string, string>())
                .SetHeaders(headers ?? new Dictionary<string, string>())
                .SetHeader("Referer", SiteLink)
                .WithRateLimit(_rateLimit.TotalSeconds)
                .Build();

            landingResult = await HttpClient.ExecuteProxiedAsync(request, Definition);

            Cookies = landingResult.GetCookies();

            // Some sites have a temporary redirect before the login page, we need to process it.
            //if (_definition.Followredirect)
            //{
            //    await FollowIfRedirect(landingResult, loginUrl.AbsoluteUri, overrideCookies: landingResult.Cookies, accumulateCookies: true);
            //}
            var htmlParser = new HtmlParser();
            landingResultDocument = htmlParser.ParseDocument(landingResult.Content);

            Captcha captcha = null;

            if (login.Captcha != null)
            {
                captcha = await GetCaptcha(login);
            }

            if (captcha != null && automaticLogin)
            {
                _logger.Error("CardigannIndexer ({0}): Found captcha during automatic login, aborting", _definition.Id);
            }

            return captcha;
        }

        private async Task<Captcha> GetCaptcha(LoginBlock login)
        {
            var captcha = login.Captcha;

            var variables = GetBaseTemplateVariables();
            var headers = ParseCustomHeaders(_definition.Login?.Headers ?? _definition.Search?.Headers, variables);

            if (captcha.Type == "image")
            {
                var captchaElement = landingResultDocument.QuerySelector(captcha.Selector);
                if (captchaElement != null)
                {
                    var loginUrl = ResolvePath(login.Path);
                    var captchaUrl = ResolvePath(captchaElement.GetAttribute("src"), loginUrl);

                    var request = new HttpRequestBuilder(captchaUrl.ToString())
                        .SetCookies(Cookies ?? new Dictionary<string, string>())
                        .SetHeaders(headers ?? new Dictionary<string, string>())
                        .SetHeader("Referer", loginUrl.AbsoluteUri)
                        .SetEncoding(_encoding)
                        .WithRateLimit(_rateLimit.TotalSeconds)
                        .Build();

                    var response = await HttpClient.ExecuteProxiedAsync(request, Definition);

                    if (response.GetCookies().Any())
                    {
                        Cookies = response.GetCookies();
                    }

                    return new Captcha
                    {
                        ContentType = response.Headers.ContentType,
                        ImageData = response.ResponseData
                    };
                }

                _logger.Debug("CardigannIndexer ({0}): No captcha image found", _definition.Id);
            }
            else
            {
                throw new NotImplementedException(string.Format("Captcha type \"{0}\" is not implemented", captcha.Type));
            }

            return null;
        }

        protected string GetRedirectDomainHint(string requestUrl, string redirectUrl)
        {
            var siteLinkUri = new HttpUri(SiteLink);
            var requestUri = new HttpUri(requestUrl);
            var redirectUri = new HttpUri(redirectUrl);

            if (requestUri.Host.StartsWith(siteLinkUri.Host) && !redirectUri.Host.StartsWith(siteLinkUri.Host))
            {
                return redirectUri.Scheme + "://" + redirectUri.Host + "/";
            }

            return null;
        }

        protected string GetRedirectDomainHint(HttpResponse result) => GetRedirectDomainHint(result.Request.Url.ToString(), result.RedirectUrl);

        protected async Task<HttpResponse> HandleRequest(RequestBlock request, Dictionary<string, object> variables = null, string referer = null)
        {
            var requestLinkStr = ResolvePath(ApplyGoTemplateText(request.Path, variables)).ToString();

            Dictionary<string, string> pairs = null;
            var queryCollection = new NameValueCollection();

            var method = HttpMethod.Get;
            if (string.Equals(request.Method, "post", StringComparison.OrdinalIgnoreCase))
            {
                method = HttpMethod.Post;
                pairs = new Dictionary<string, string>();
            }

            if (request.Inputs != null)
            {
                foreach (var input in request.Inputs)
                {
                    var value = ApplyGoTemplateText(input.Value, variables);
                    if (method == HttpMethod.Get)
                    {
                        queryCollection.Add(input.Key, value);
                    }
                    else if (method == HttpMethod.Post)
                    {
                        pairs.Add(input.Key, value);
                    }
                }
            }

            if (queryCollection.Count > 0)
            {
                if (!requestLinkStr.Contains('?'))
                {
                    requestLinkStr += "?";
                }

                requestLinkStr += queryCollection.GetQueryString(_encoding, separator: request.Queryseparator);
            }

            var httpRequestBuilder = new HttpRequestBuilder(requestLinkStr)
            {
                Method = method,
                Encoding = _encoding
            };

            // Add form data for POST requests
            if (method == HttpMethod.Post)
            {
                foreach (var param in pairs)
                {
                    httpRequestBuilder.AddFormParameter(param.Key, param.Value);
                }
            }

            var headers = ParseCustomHeaders(_definition.Download?.Headers ?? _definition.Search?.Headers, variables);

            var httpRequest = httpRequestBuilder
                .SetCookies(Cookies ?? new Dictionary<string, string>())
                .SetHeaders(headers ?? new Dictionary<string, string>())
                .SetHeader("Referer", referer)
                .WithRateLimit(_rateLimit.TotalSeconds)
                .Build();

            _logger.Debug("CardigannIndexer ({0}): handleRequest() httpRequest={1}", _definition.Id, httpRequest);

            var response = await HttpClient.ExecuteProxiedAsync(httpRequest, Definition);

            _logger.Debug("CardigannIndexer ({0}): handleRequest() remote server returned {1}", _definition.Id, response.StatusCode);

            return response;
        }

        public async Task<HttpRequest> DownloadRequest(Uri link)
        {
            Cookies = GetCookies();
            var method = HttpMethod.Get;

            var variables = GetBaseTemplateVariables();
            AddTemplateVariablesFromUri(variables, link, ".DownloadUri");
            var headers = ParseCustomHeaders(_definition.Download?.Headers ?? _definition.Search?.Headers, variables);

            if (_definition.Download != null)
            {
                var download = _definition.Download;

                HttpResponse response = null;

                var request = new HttpRequestBuilder(link.ToString())
                    .SetCookies(Cookies ?? new Dictionary<string, string>())
                    .SetHeaders(headers ?? new Dictionary<string, string>())
                    .SetEncoding(_encoding)
                    .WithRateLimit(_rateLimit.TotalSeconds)
                    .Build();

                request.AllowAutoRedirect = true;

                var beforeBlock = download.Before;
                if (beforeBlock != null)
                {
                    if (beforeBlock.Pathselector != null)
                    {
                        response = await HttpClient.ExecuteProxiedAsync(request, Definition);
                        beforeBlock.Path = MatchSelector(response, beforeBlock.Pathselector, variables);
                    }

                    response = await HandleRequest(beforeBlock, variables, link.ToString());
                }

                if (download.Method == "post")
                {
                    method = HttpMethod.Post;
                }

                if (download.Infohash != null)
                {
                    try
                    {
                        if (!download.Infohash.Usebeforeresponse || download.Before == null || response == null)
                        {
                            response = await HttpClient.ExecuteProxiedAsync(request, Definition);
                        }

                        var hash = MatchSelector(response, download.Infohash.Hash, variables);
                        if (hash == null)
                        {
                            throw new CardigannException("InfoHash selectors didn't match hash.");
                        }

                        var title = MatchSelector(response, download.Infohash.Title, variables);
                        if (title == null)
                        {
                            throw new CardigannException("InfoHash selectors didn't match title.");
                        }

                        var magnet = MagnetLinkBuilder.BuildPublicMagnetLink(hash, title);
                        var torrentLink = ResolvePath(magnet, link);

                        var hashDownloadRequest = new HttpRequestBuilder(torrentLink.AbsoluteUri)
                            .SetCookies(Cookies ?? new Dictionary<string, string>())
                            .SetHeaders(headers ?? new Dictionary<string, string>())
                            .SetEncoding(_encoding)
                            .Build();

                        hashDownloadRequest.Method = method;

                        return hashDownloadRequest;
                    }
                    catch (Exception)
                    {
                        _logger.Error("CardigannIndexer ({0}): Exception with InfoHash block with hashSelector {1} and titleSelector {2}",
                            _definition.Id,
                            download.Infohash.Hash.Selector,
                            download.Infohash.Title.Selector);
                    }
                }
                else if (download.Selectors != null)
                {
                    foreach (var selector in download.Selectors)
                    {
                        var queryselector = ApplyGoTemplateText(selector.Selector, variables);

                        try
                        {
                            if (!selector.Usebeforeresponse || download.Before == null || response == null)
                            {
                                response = await HttpClient.ExecuteProxiedAsync(request, Definition);
                            }

                            var href = MatchSelector(response, selector, variables, debugMatch: true);
                            if (href == null)
                            {
                                continue;
                            }

                            var torrentLink = ResolvePath(href, link);
                            if (torrentLink.Scheme != "magnet" && _definition.TestLinkTorrent)
                            {
                                // Test link
                                var testLinkRequest = new HttpRequestBuilder(torrentLink.ToString())
                                    .SetCookies(Cookies ?? new Dictionary<string, string>())
                                    .SetHeaders(headers ?? new Dictionary<string, string>())
                                    .SetEncoding(_encoding)
                                    .WithRateLimit(_rateLimit.TotalSeconds)
                                    .Build();

                                response = await HttpClient.ExecuteProxiedAsync(testLinkRequest, Definition);

                                var content = response.Content;
                                if (content.Length >= 1 && content[0] != 'd')
                                {
                                    _logger.Debug("CardigannIndexer ({0}): Download selector {1}'s torrent file is invalid, retrying with next available selector", _definition.Id, queryselector);

                                    continue;
                                }
                            }

                            link = torrentLink;

                            var selectorDownloadRequest = new HttpRequestBuilder(link.AbsoluteUri)
                                .SetCookies(Cookies ?? new Dictionary<string, string>())
                                .SetHeaders(headers ?? new Dictionary<string, string>())
                                .SetEncoding(_encoding)
                                .WithRateLimit(_rateLimit.TotalSeconds)
                                .Build();

                            selectorDownloadRequest.Method = method;

                            return selectorDownloadRequest;
                        }
                        catch (Exception e)
                        {
                            _logger.Error("{0} CardigannIndexer ({1}): An exception occurred while trying selector {2}, retrying with next available selector", e, _definition.Id, queryselector);

                            throw new CardigannException(string.Format("An exception occurred while trying selector {0}", queryselector));
                        }
                    }
                }
            }

            var downloadRequest = new HttpRequestBuilder(link.AbsoluteUri)
                .SetCookies(Cookies ?? new Dictionary<string, string>())
                .SetHeaders(headers ?? new Dictionary<string, string>())
                .SetEncoding(_encoding)
                .WithRateLimit(_rateLimit.TotalSeconds)
                .Build();

            downloadRequest.Method = method;

            return downloadRequest;
        }

        protected string MatchSelector(HttpResponse response, SelectorField selector, Dictionary<string, object> variables, bool debugMatch = false)
        {
            var selectorText = ApplyGoTemplateText(selector.Selector, variables);
            var parser = new HtmlParser();

            var resultDocument = parser.ParseDocument(response.Content);

            var element = resultDocument.QuerySelector(selectorText);
            if (element == null)
            {
                _logger.Debug($"CardigannIndexer ({_definition.Id}): Selector {selectorText} could not match any elements.");
                return null;
            }

            if (debugMatch)
            {
                _logger.Debug($"CardigannIndexer ({_definition.Id}): Download selector {selector} matched:{element.ToHtmlPretty()}");
            }

            string val;
            if (selector.Attribute != null)
            {
                val = element.GetAttribute(selector.Attribute);
                if (val == null)
                {
                    throw new CardigannException($"Attribute \"{selector.Attribute}\" is not set for element {element.ToHtmlPretty()}");
                }
            }
            else
            {
                val = element.TextContent;
            }

            val = ApplyFilters(val, selector.Filters, variables);
            return val;
        }

        public bool CheckIfLoginIsNeeded(HttpResponse response)
        {
            if (_definition.Login == null)
            {
                return false;
            }

            if (response.HasHttpRedirect)
            {
                var domainHint = GetRedirectDomainHint(response);
                if (domainHint != null)
                {
                    var errormessage = "Got redirected to another domain. Try changing the indexer URL to " + domainHint + ".";

                    _logger.Warn(errormessage);
                }

                return true;
            }

            if (response.HasHttpError)
            {
                return true;
            }

            // Only run html test selector on html responses
            if (_definition.Login.Test?.Selector != null && (response.Headers.ContentType?.Contains("text/html") ?? true))
            {
                var parser = new HtmlParser();
                var document = parser.ParseDocument(response.Content);

                var selection = document.QuerySelectorAll(_definition.Login.Test.Selector);
                if (selection.Length == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<IndexerRequest> GetRequest(Dictionary<string, object> variables, SearchCriteriaBase searchCriteria)
        {
            var search = _definition.Search;

            var mappedCategories = _categories.MapTorznabCapsToTrackers((int[])variables[".Query.Categories"]);
            if (mappedCategories.Count == 0)
            {
                mappedCategories = _defaultCategories;
            }

            variables[".Categories"] = mappedCategories;

            var keywordTokens = new List<string>();
            var keywordTokenKeys = new List<string> { "Q", "Series", "Movie", "Year" };
            foreach (var key in keywordTokenKeys)
            {
                var value = (string)variables[".Query." + key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    keywordTokens.Add(value);
                }
            }

            if (!string.IsNullOrWhiteSpace((string)variables[".Query.Episode"]))
            {
                keywordTokens.Add((string)variables[".Query.Episode"]);
            }

            variables[".Query.Keywords"] = string.Join(" ", keywordTokens);
            variables[".Keywords"] = ApplyFilters((string)variables[".Query.Keywords"], search.Keywordsfilters, variables);

            // TODO: prepare queries first and then send them parallel
            var searchPaths = search.Paths;
            foreach (var searchPath in searchPaths)
            {
                // skip path if categories don't match
                if (searchPath.Categories != null && mappedCategories.Count > 0)
                {
                    var invertMatch = searchPath.Categories[0] == "!";
                    var hasIntersect = mappedCategories.Intersect(searchPath.Categories).Any();
                    if (invertMatch)
                    {
                        hasIntersect = !hasIntersect;
                    }

                    if (!hasIntersect)
                    {
                        continue;
                    }
                }

                // build search URL
                // HttpUtility.UrlPathEncode seems to only encode spaces, we use UrlEncode and replace + with %20 as a workaround
                var searchUrl = ResolvePath(ApplyGoTemplateText(searchPath.Path, variables, WebUtility.UrlEncode).Replace("+", "%20")).AbsoluteUri;
                var queryCollection = new List<KeyValuePair<string, string>>();
                var method = HttpMethod.Get;

                if (string.Equals(searchPath.Method, "post", StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Post;
                }

                var inputsList = new List<Dictionary<string, string>>();
                if (searchPath.Inheritinputs)
                {
                    inputsList.Add(search.Inputs);
                }

                inputsList.Add(searchPath.Inputs);

                foreach (var inputs in inputsList)
                {
                    if (inputs != null)
                    {
                        foreach (var input in inputs)
                        {
                            if (input.Key == "$raw")
                            {
                                var rawStr = ApplyGoTemplateText(input.Value, variables, WebUtility.UrlEncode);
                                foreach (var part in rawStr.Split('&'))
                                {
                                    var parts = part.Split(new[] { '=' }, 2);
                                    var key = parts[0];
                                    if (key.Length == 0)
                                    {
                                        continue;
                                    }

                                    var value = "";
                                    if (parts.Length == 2)
                                    {
                                        value = parts[1];
                                    }

                                    queryCollection.Add(key, value);
                                }
                            }
                            else
                            {
                                var inputValue = ApplyGoTemplateText(input.Value, variables);

                                if (inputValue.IsNotNullOrWhiteSpace() || search.AllowEmptyInputs)
                                {
                                    queryCollection.Add(input.Key, inputValue);
                                }
                            }
                        }
                    }
                }

                if (method == HttpMethod.Get)
                {
                    if (queryCollection.Count > 0)
                    {
                        searchUrl += "?" + queryCollection.GetQueryString(_encoding);
                    }
                }

                _logger.Debug($"Adding request: {searchUrl}");

                var requestBuilder = new HttpRequestBuilder(searchUrl)
                {
                    Method = method,
                    Encoding = _encoding
                };

                // Add FormData for searchs that POST
                if (method == HttpMethod.Post)
                {
                    foreach (var param in queryCollection)
                    {
                        requestBuilder.AddFormParameter(param.Key, param.Value);
                    }
                }

                // send HTTP request
                if (search.Headers != null)
                {
                    var headers = ParseCustomHeaders(search.Headers, variables);
                    requestBuilder.SetHeaders(headers ?? new Dictionary<string, string>());
                }

                var request = requestBuilder
                    .WithRateLimit(_rateLimit.TotalSeconds)
                    .Build();

                var cardigannRequest = new CardigannRequest(request, variables, searchPath)
                    {
                        HttpRequest =
                        {
                            AllowAutoRedirect = searchPath.Followredirect
                        }
                    };

                yield return cardigannRequest;
            }
        }
    }
}
