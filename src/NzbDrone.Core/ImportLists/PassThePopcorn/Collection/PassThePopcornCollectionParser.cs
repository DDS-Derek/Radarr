using System.Collections.Generic;
using System.Linq;
using System.Net;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.ImportLists.ImportListMovies;

namespace NzbDrone.Core.ImportLists.PassThePopcorn.Collection;

public class PassThePopcornCollectionParser : IParseImportListResponse
{
    public IList<ImportListMovie> ParseResponse(ImportListResponse importListResponse)
    {
        var jsonResponse =  STJson.Deserialize<PassThePopcornCollectionResponse>(importListResponse.Content);

        return jsonResponse.CoverView
            .Movies
            .Select(item => new ImportListMovie
            {
                ImdbId = item.ImdbId,
                Title = WebUtility.HtmlDecode(item.Title),
                Year = int.Parse(item.Year)
            })
            .ToList();
    }
}
