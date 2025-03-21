﻿using Data.Base.Models;
using Data.Mongo.Config;
using Data.Mongo.Wrappers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Linq;

namespace Data.Mongo.Tests.Development;

// For local testing use docker-compose, or add a docker instance of mongo with
// ```sh
// docker network create test-mongo-cluster
// docker run --name mongo1 -p 27021:27017 --net test-mongo-cluster -d mongo:latest mongod --replSet test-mongo-set
// docker run --name mongo2 -p 27022:27017 --net test-mongo-cluster -d mongo:latest mongod --replSet test-mongo-set
// docker exec -it mongo1 mongosh
// ```
// and configure it, then comment out the "Ignore" attribute below.
[Ignore("Disabled for CI pipelines, only for local testing")]
public sealed class MongoDbIntegrationTests : IDisposable
{
    private static readonly string _testId = "1";
    private static readonly string _testTopic = "Topic";
    private static readonly string _testRegion = "Unknown";
    private static readonly DatastoreItem _jobItemStub = new() { Id = _testId };
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug).AddDebug());
    private static readonly ILogger<JobQueueMongoService> _logger = _loggerFactory.CreateLogger<JobQueueMongoService>();
    private readonly JobQueueMongoService _dbService;

    public MongoDbIntegrationTests()
    {
        // Arrange
        var mongoDbOptions = new MongoDbOptions
        {
            DbRegion = _testRegion,
            DbRecordExpiry = TimeSpan.Zero,
            MongoDbEndpoint = "mongodb://localhost:27017",
        };
        var mongoClientWrapper = new WorkItemMongoClientWrapper(mongoDbOptions, _logger);
        _dbService = new JobQueueMongoService(mongoClientWrapper, _logger);
    }

    [OneTimeSetUp]
    public async Task SetupAsync()
    {
        var success = await _dbService.CreateAsync(_jobItemStub, CancellationToken.None).ConfigureAwait(false);
        Assert.That(success, Is.True);
    }

    [TestCase(1)]
    public async Task UpdateReadAsync_TestsMongoDbSetup_ExpectsValidDatastoreItem(byte progress)
    {
        var jobItemStub = _jobItemStub with { Progress = progress };
        var success = await _dbService.UpdateAsync(jobItemStub, CancellationToken.None);
        Assert.That(success, Is.True);
        var result = await _dbService.ReadAsync(_testId, _testTopic, CancellationToken.None);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.AssignableFrom<DatastoreItem>());
        Assert.That(result.Progress, Is.EqualTo(progress));
    }

    [OneTimeTearDown]
    public async Task TeardownAsync()
    {
        var success = await _dbService.DeleteAsync(_jobItemStub, CancellationToken.None).ConfigureAwait(false);
        Assert.That(success, Is.True);
    }

    [Test]
    public async Task QueryAsync_TestsMongoDbSetup_ExpectsValidDatastoreItem()
    {
        var query = await _dbService.QueryAsync().ConfigureAwait(false);
        var result = query.AsEnumerable().FirstOrDefault();
        Assert.That(result, Is.Not.Null);
        var count = query.Count();
        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public async Task WatchChangesAsync_TestsMongoDbSetup_ExpectsValidDatastoreItem()
    {
        await foreach (var jobItem in _dbService.MonitorAsync(_testId).ConfigureAwait(false))
        {
            Assert.That(jobItem, Is.Not.Null);
            Assert.That(jobItem, Is.AssignableFrom<DatastoreItem>());
            break;
        }
    }

    //[Test]
    //public async Task JobUpdates_TestsChangeStream_ExpectsValidDatastoreItem()
    //{
    //    // Act
    //    await foreach (var jobItem in _dbService.JobUpdates.Value.ToAsyncEnumerable().ConfigureAwait(false))
    //    {
    //        // Assert
    //        Assert.That(jobItem, Is.Not.Null);
    //        Assert.That(jobItem, Is.AssignableFrom<DatastoreItem>());
    //        break;
    //    }
    //}

    public void Dispose()
    {
        _dbService.Dispose();
    }
}
