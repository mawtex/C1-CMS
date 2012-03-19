﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using Composite.Core.Extensions;
using Composite.Core.WebClient;
using Composite.Data;
using Composite.Data.Types;

namespace Composite.Core.Routing
{
    /// <summary>    
    /// Responsible for parsing and building media urls
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public static class MediaUrls
    {
        internal static readonly string DefaultMediaStore = "MediaArchive";
        private static readonly string MediaUrl_UnprocessedInternalPrefix = "~/media(";
        private static readonly string MediaUrl_InternalPrefix = UrlUtils.PublicRootPath + "/media(";
        private static readonly string MediaUrl_PublicPrefix = UrlUtils.PublicRootPath + "/media/";

        private static readonly string ForbiddenUrlCharacters = @"<>*%&\?#";

        /// <exclude />
        public static MediaUrlData ParseUrl(string relativeUrl)
        {
            UrlKind urlKind;
            return ParseUrl(relativeUrl, out urlKind);
        }

        /// <exclude />
        public static MediaUrlData ParseUrl(string relativeUrl, out UrlKind urlKind)
        {
            urlKind = UrlKind.Undefined;

            bool isInternalLink = relativeUrl.StartsWith(MediaUrl_InternalPrefix, StringComparison.Ordinal);
            bool isInternalUnprocessedLink = !isInternalLink && relativeUrl.StartsWith(MediaUrl_UnprocessedInternalPrefix, StringComparison.Ordinal);

            if (isInternalLink || isInternalUnprocessedLink)
            {
                string prefix = isInternalLink ? MediaUrl_InternalPrefix : MediaUrl_UnprocessedInternalPrefix;

                var result = ParseInternalUrl(relativeUrl, prefix);
                if(result != null)
                {
                    urlKind = UrlKind.Internal;
                } 

                return result;
            }

            int minimumLengthOfPublicMediaUrl = MediaUrl_PublicPrefix.Length + 36; /* 36 - length of a guid */
            if (relativeUrl.Length >= minimumLengthOfPublicMediaUrl
                && relativeUrl.StartsWith(MediaUrl_PublicPrefix))
            {
                // Parsing urls like /<site root>/media/{MediaId}*
                Guid mediaId;

                if (Guid.TryParse(relativeUrl.Substring(MediaUrl_PublicPrefix.Length, 36), out mediaId))
                {
                    NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

                    urlKind = UrlKind.Public;
                    return new MediaUrlData
                    {
                        MediaId = mediaId,
                        MediaStore = DefaultMediaStore,
                        QueryParameters = queryParams
                    };
                }

                // Parsing urls like /<site root>/media/<MediaArchive>/{MediaId}*
                int slashOffset = relativeUrl.IndexOf('/', MediaUrl_PublicPrefix.Length + 1);
                if (slashOffset > MediaUrl_PublicPrefix.Length + 1 && relativeUrl.Length >= slashOffset + 36
                    && Guid.TryParse(relativeUrl.Substring(slashOffset + 1, 36), out mediaId))
                {
                    string mediaStore = relativeUrl.Substring(MediaUrl_PublicPrefix.Length, slashOffset - MediaUrl_PublicPrefix.Length);

                    NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

                    urlKind = UrlKind.Public;
                    return new MediaUrlData
                    {
                        MediaId = mediaId,
                        MediaStore = mediaStore,
                        QueryParameters = queryParams
                    };
                }
            }

            return null;
        }

        private static MediaUrlData ParseInternalUrl(string relativeUrl, string urlPrefix)
        {
            int endBracketOffset = relativeUrl.IndexOf(")", StringComparison.Ordinal);
            if (endBracketOffset < 0) return null;

            string mediaStoreAndId = relativeUrl.Substring(urlPrefix.Length, endBracketOffset - urlPrefix.Length);

            string store = null;
            string mediaIdStr;

            int separatorIndex = mediaStoreAndId.LastIndexOf(":", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                store = mediaStoreAndId.Substring(0, separatorIndex);
                mediaIdStr = mediaStoreAndId.Substring(separatorIndex + 1);
            }
            else
            {
                mediaIdStr = mediaStoreAndId;
            }

            Guid mediaId;
            if (!Guid.TryParse(mediaIdStr, out mediaId))
            {
                return null;
            }

            NameValueCollection queryParams = new UrlBuilder(relativeUrl).GetQueryParameters();

            return new MediaUrlData
                       {
                           MediaId = mediaId,
                           MediaStore = store ?? DefaultMediaStore,
                           QueryParameters = queryParams
                       };
        }

        /// <exclude />
        public static string BuildUrl(MediaUrlData mediaUrlData, UrlKind urlKind)
        {
            Verify.ArgumentNotNull(mediaUrlData, "mediaUrlData");

            switch (urlKind)
            {
                case UrlKind.Renderer:
                    return BuildRendererUrl(mediaUrlData);
                case UrlKind.Public:
                    return BuildPublicUrl(mediaUrlData);
            }

            throw new NotSupportedException("Not supported url kind. urlKind == '0'".FormatWith(urlKind));
        }

        private static string BuildRendererUrl(MediaUrlData mediaUrlData)
        {
            var queryParams = new NameValueCollection(mediaUrlData.QueryParameters);

            queryParams.Add("id", mediaUrlData.MediaId.ToString());

            if (mediaUrlData.MediaStore != null 
                && mediaUrlData.MediaStore != DefaultMediaStore)
            {
                queryParams.Add("store", mediaUrlData.MediaStore);
            }

            var url = new UrlBuilder(UrlUtils.PublicRootPath + "/Renderers/ShowMedia.ashx");
            url.AddQueryParameters(queryParams);

            return url;
        }

        private static string BuildPublicUrl(MediaUrlData mediaUrlData)
        {
            NameValueCollection queryParams = new NameValueCollection(mediaUrlData.QueryParameters);

            IMediaFile file = GetFileById(mediaUrlData.MediaStore, mediaUrlData.MediaId);
            if (file == null)
            {
                return null;
            }

            string pathToFile = UrlUtils.Combine(file.FolderPath, file.FileName);

            pathToFile = RemoveForbiddenCharactersAndNormalize(pathToFile);

            // IIS6 doesn't have wildcard mapping by default, so removing image extension if running in "classic" app pool
            if (!HttpRuntime.UsingIntegratedPipeline)
            {
                int dotOffset = pathToFile.IndexOf(".", StringComparison.Ordinal);
                if (dotOffset >= 0)
                {
                    pathToFile = pathToFile.Substring(0, dotOffset);
                }
            }

            string mediaStore = string.Empty;

            if(!mediaUrlData.MediaStore.Equals(DefaultMediaStore, StringComparison.InvariantCultureIgnoreCase))
            {
                mediaStore = mediaUrlData.MediaStore + "/";
            }


            var url = new UrlBuilder(UrlUtils.PublicRootPath + "/media/" + mediaStore + mediaUrlData.MediaId);

            if (pathToFile.Length > 0)
            {
                url.PathInfo = pathToFile;
            }
            url.AddQueryParameters(queryParams);

            return url.ToString();
        }

        private static string RemoveForbiddenCharactersAndNormalize(string path)
        {
            foreach (var ch in ForbiddenUrlCharacters)
            {
                path = path.Replace(ch, '#');
            }

            path = path.Replace("#", string.Empty);

            // Removing consequtive white spaces
            while(path.Contains("  "))
            {
                path = path.Replace("  ", " ");
            }

            string[] parts = path.Split('/');

            var result = new StringBuilder();
            for(int i=0; i<parts.Length; i++)
            {
                string trimmedPart = parts[i].Trim();
                if(trimmedPart.Length > 0)
                {
                    result.Append("/").Append(trimmedPart);
                }
            }

            return result.ToString();
        }

        private static IMediaFile GetFileById(string storeId, Guid fileId)
        {
            using (new DataScope(DataScopeIdentifier.Public))
            {
                var query = DataFacade.GetData<IMediaFile>();

                if (query.IsEnumerableQuery())
                {
                    return (query as IEnumerable<IMediaFile>)
                        .FirstOrDefault(f => f.StoreId == storeId && f.Id == fileId);
                }

                return query
                    .FirstOrDefault(f => f.StoreId == storeId && f.Id == fileId);
            }
        }
    }
}
