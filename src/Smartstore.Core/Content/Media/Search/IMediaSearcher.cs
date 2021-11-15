﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Smartstore.Collections;

namespace Smartstore.Core.Content.Media
{
    public interface IMediaSearcher
    {
        IPagedList<MediaFile> SearchFiles(MediaSearchQuery query, MediaLoad flags = MediaLoad.AsNoTracking);
        IQueryable<MediaFile> PrepareQuery(MediaSearchQuery query, MediaLoad flags);
        IQueryable<MediaFile> ApplyFilterQuery(MediaFilesFilter filter, IQueryable<MediaFile> sourceQuery = null);
        IQueryable<MediaFile> ApplyLoadFlags(IQueryable<MediaFile> query, MediaLoad flags);
    }
}
