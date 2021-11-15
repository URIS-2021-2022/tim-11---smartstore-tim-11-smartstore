﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;

namespace Smartstore.Core.Seo
{
    internal class UrlServiceBatchScope : Disposable, IUrlServiceBatchScope
    {
        private UrlService _urlService;
        private readonly SmartDbContext _db;
        private DbSet<UrlRecord> _dbSet;
        private readonly List<ValidateSlugResult> _batch = new();

        public UrlServiceBatchScope(UrlService urlService, SmartDbContext db = null)
        {
            _urlService = urlService.GetInstanceForForBatching(db);
            _db = urlService._db;
            _dbSet = _db.UrlRecords;
        }

        public virtual async Task<string> GetActiveSlugAsync(int entityId, string entityName, int languageId)
        {
            Guard.NotEmpty(entityName, nameof(entityName));

            if (_urlService.TryGetPrefetchedActiveSlug(entityId, entityName, languageId, out var slug))
            {
                return slug;
            }

            // Don't involve cache as it could put high memory pressure on app.
            var query = _dbSet
                .ApplyEntityFilter(entityName, entityId, languageId, true)
                .Select(x => x.Slug);

            return (await query.FirstOrDefaultAsync()).EmptyNull();
        }

        public virtual void ApplySlugs(params ValidateSlugResult[] slugs)
        {
            _batch.AddRange(slugs);
        }

        public virtual async Task<int> CommitAsync(CancellationToken cancelToken = default)
        {
            if (_batch.Count == 0)
                return 0;
            
            var batch = await ValidateBatchAsync(_batch, cancelToken);

            var batchByEntityName = batch.ToMultimap(x => x.EntityName, x => x);

            foreach (var kvp in batchByEntityName)
            {
                var entityName = kvp.Key;
                var slugs = kvp.Value;
                var languageIds = slugs.Select(x => x.LanguageId ?? 0).Distinct().ToArray();
                var entityIds = slugs.Select(x => x.Source.Id).Where(x => x > 0).Distinct().ToArray();

                var collection = await _urlService.GetUrlRecordCollectionAsync(entityName, languageIds, entityIds, tracked: true);

                foreach (var slug in slugs)
                {
                    await _urlService.ApplySlugAsync(slug, collection, false);
                }
            }

            var numAffected = await _urlService._db.SaveChangesAsync(cancelToken);
            _batch.Clear();
            return numAffected;
        }

        private async Task<List<ValidateSlugResult>> ValidateBatchAsync(IList<ValidateSlugResult> batch, CancellationToken cancelToken = default)
        {
            var batch2 = batch.Where(x => x.Source != null && x.Slug.HasValue());

            // TODO: (core) Refactor UrlServiceBatchScope.ValidateBatchAsync > uniqueness is not guaranteed within a large batch.
            // Idea: Catch UniquenessViolationException and validate only then.

            var unvalidatedSlugsMap = batch2
                .Where(x => !x.WasValidated)
                //.ToDictionarySafe(x => x.Slug, x => x.Found, StringComparer.OrdinalIgnoreCase);
                //.Select(x => x.Slug)
                .DistinctBy(x => x.Slug)
                .ToDictionary(x => x.Slug, x => x.Found, StringComparer.OrdinalIgnoreCase);

            var unvalidatedSlugs = unvalidatedSlugsMap.Keys;

            var foundRecords = await _dbSet.Where(x => unvalidatedSlugs.Contains(x.Slug)).ToListAsync();

            foundRecords.Each(x => unvalidatedSlugsMap[x.Slug] = x);
            _urlService._extraSlugLookup.Each(x => unvalidatedSlugsMap[x.Key] = x.Value);

            var validatedBatch = batch2
                .SelectAsync(async (slug) =>
                {
                    if (slug.WasValidated)
                    {
                        return slug;
                    }

                    var isReserved = _urlService._seoSettings.ReservedUrlRecordSlugs.Contains(slug.Slug);
                    var found = unvalidatedSlugsMap.Get(slug.Slug);
                    var foundIsSelf = _urlService.FoundRecordIsSelf(slug.Source, found, slug.LanguageId);
                    
                    if (foundIsSelf)
                    {
                        return new ValidateSlugResult(slug) { WasValidated = true, Found = found, FoundIsSelf = true };
                    }

                    if (found != null || isReserved)
                    {
                        // The existence of a UrlRecord instance for a given slug implies that the slug
                        // needs to run through the default validation process
                        // to ensure uniqueness.
                        return await _urlService.ValidateSlugAsync(slug.Source, slug.Slug, true, slug.LanguageId).AsTask();
                    }

                    return new ValidateSlugResult(slug) { WasValidated = true };
                });

            return await validatedBatch.AsyncToList(cancelToken);
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _urlService = null;
                _dbSet = null;
            }    
        }
    }
}