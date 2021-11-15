using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Smartstore.Web.Bundling
{
    public static class BundleProcessorCodes
    {
        public const string Minify = "min";
        public const string Autoprefix = "autoprefix";
        public const string UrlRewrite = "urlrewrite";
    }

    public interface IBundleProcessor
    {
        string Code { get; }
        void PopulateCacheKey(Bundle bundle, HttpContext httpContext, IDictionary<string, string> values);
        Task ProcessAsync(BundleContext context);
    }

    public abstract class BundleProcessor : IBundleProcessor
    {
        public virtual string Code
        {
            get => string.Empty;
        }
        
        public virtual void PopulateCacheKey(Bundle bundle, HttpContext httpContext, IDictionary<string, string> values)
        {
        }

        public abstract Task ProcessAsync(BundleContext context);
    }
}
