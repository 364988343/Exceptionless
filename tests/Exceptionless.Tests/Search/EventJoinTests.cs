using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories {
    public sealed class EventJoinTests : IntegrationTestsBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;

        public EventJoinTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            TestSystemClock.SetFrozenTime(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
            _stackRepository = GetService<IStackRepository>();
            _eventRepository = GetService<IEventRepository>();
        }

        [Theory]
        [InlineData("status:fixed", 2)]
        [InlineData("status:regressed", 1)]
        [InlineData("@stack(status:open)", 1)]
        public async Task GetByStatusAsync(string filter, int count) {
            await CreateTestData(d => {
                d.AddErrorEvent().Fixed("1.2.3");
                d.AddErrorEvent();
            });

            var result = await _eventRepository.QueryAsync(q => q.FilterExpression(filter));
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        [Fact]
        public async Task CanQueryFixedStatus() {
            var result = await _eventRepository.QueryAsync(q => q.FilterExpression("status:open"));
            Assert.NotNull(result);
            Assert.Equal(1, result.Total);
        }

        [Theory]
        [InlineData("is_fixed:true", 1)]
        [InlineData("status:fixed", 1)] // Returns 1 because there is two fixed stacks but only one fixed event.
        [InlineData("@stack:(status:fixed)", 1)] // Returns 1 because there is two fixed stacks but only one fixed event.
        [InlineData("tags:old_tag", 0)] // Stack only tags won't be resolved
        [InlineData("type:log status:fixed", 1)]
        [InlineData("type:log version_fixed:1.2.3", 0)] // Stack 3ecd0826e447a44e78877ab3 has no events.
        [InlineData("type:error is_hidden:false is_fixed:false is_regressed:true", 1)]
        [InlineData("type:log status:fixed @stack:(version_fixed:1.2.3)", 0)] // Stack 3ecd0826e447a44e78877ab3 has no events.
        [InlineData("54dbc16ca0f5c61398427b00", 1)] // Event Id
        [InlineData("1ecd0826e447a44e78877ab1", 0)] // Stack Id
        [InlineData("type:error", 2)]
        public async Task GetByJoinFilterAsync(string filter, int count) {
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);
            Log.SetLogLevel<StackRepository>(LogLevel.Trace);
            Log.SetLogLevel<EventJoinFilterVisitor>(LogLevel.Trace);
            Log.SetLogLevel<StackFieldResolverQueryVisitor>(LogLevel.Trace);

            var result = await _eventRepository.QueryAsync(q => q.FilterExpression(filter));
            Assert.NotNull(result);
            Assert.Equal(count, result.Total);
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();

            var oldLoggingLevel = Log.MinimumLevel;
            Log.MinimumLevel = LogLevel.Warning;

            await StackData.CreateSearchDataAsync(_stackRepository, GetService<JsonSerializer>());
            await EventData.CreateSearchDataAsync(GetService<ExceptionlessElasticConfiguration>(), _eventRepository, GetService<EventParserPluginManager>());

            Log.MinimumLevel = oldLoggingLevel;
        }
    }
}