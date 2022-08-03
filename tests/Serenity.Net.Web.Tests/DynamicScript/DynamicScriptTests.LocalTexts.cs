using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serenity.ComponentModel;
using Serenity.Localization;
using Serenity.Web.Middleware;
using System.IO;
using System.Threading.Tasks;

namespace Serenity.Tests.Web
{
    public partial class DynamicScriptTests
    {    
        [Fact]
        public async Task LocalTexts_Validate_Status_Code_On_Valid_RequestAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/LocalText.Site.en-US.Public");

            Assert.Equal(200, context.Response.StatusCode);
        }
        
        [Fact]
        public async Task LocalTexts_Validate_Content_Type_On_Valid_RequestAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/LocalText.Site.en-US.Public.js");

            Assert.Equal("text/javascript; charset=utf-8", context.Response.ContentType);
        }

        [Fact]
        public async Task LocalTexts_Validate_Body_On_Valid_RequestAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/LocalText.Site.en-US.Public");

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var reader = new StreamReader(context.Response.Body);

            Assert.Equal("Q.LT.add({});", reader.ReadToEnd());
        }

        [Fact]
        public async Task LocalTexts_Validate_Status_Code_On_Invalid_LanguageIdAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/LocalText.Site.invalid-S.Public");

            Assert.NotEqual(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task LocalTexts_Validate_Status_Code_On_Invalid_PackageNameAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/LocalText.Invalid.en-US.Public");

            Assert.NotEqual(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task LocalTexts_Validate_Status_Code_On_Invalid_PrefixAsync()
        {
            var context = await CreateAndSendRequestToMiddlewareAsync("/DynJS.axd/InvalidPrefix.Site.en-US.Public");

            Assert.NotEqual(200, context.Response.StatusCode);
        }
    }
}
