using System.Collections.Generic;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.ImportLists.PassThePopcorn.Collection;

public class PassThePopcornCollectionRequestGenerator : IImportListRequestGenerator
{
    private readonly PassThePopcornCollectionSettings _settings;

    public PassThePopcornCollectionRequestGenerator(PassThePopcornCollectionSettings settings)
    {
        _settings = settings;
    }

    public ImportListPageableRequestChain GetMovies()
    {
        var pageableRequests = new ImportListPageableRequestChain();
        pageableRequests.Add(GetPagedRequests());
        return pageableRequests;
    }

    private IEnumerable<ImportListRequest> GetPagedRequests()
    {
        var requestBuilder = BuildRequest();

        for (var pageNumber = 1; pageNumber <= 2; pageNumber++)
        {
            requestBuilder.AddQueryParam("page", pageNumber, true);

            var request = requestBuilder.Build();

            yield return new ImportListRequest(request);
        }
    }

    private HttpRequestBuilder BuildRequest()
    {
        var url = _settings.CollectionUrl.TrimEnd('/');

        return new HttpRequestBuilder(url)
            .Accept(HttpAccept.Json)
            .SetHeader("ApiUser", _settings.ApiUser)
            .SetHeader("ApiKey", _settings.ApiKey)
            .AddQueryParam("action", "get_page")
            .AddQueryParam("filter_cat[1]", "1")
            .AddQueryParam("filter_cat[2]", "1")
            .AddQueryParam("filter_cat[4]", "1")
            .AddQueryParam("filter_cat[5]", "1")
            .AddQueryParam("filter_cat[6]", "1");
    }
}
