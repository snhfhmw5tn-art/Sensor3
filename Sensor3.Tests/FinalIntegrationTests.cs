using Sensor3.Contracts;
using Sensor3.Core;
using Sensor3.Infrastructure;
using Sensor3.Simulation;
namespace Sensor3.Tests;
[TestClass]
public sealed class FinalIntegrationTests
{
    [TestMethod]
    public void TestThat_replay_uses_time_marked_truck_context_for_comparison_after_seek()
    {
        var original = SyntheticScenarios.Create(SyntheticScenario.ForkliftDriving, 12);
        var frames = new[] { new RecordedFrame(0, new TelemetryEvent(Context: new(DeclaredForklift: true))) }.Concat(original.Frames).ToArray();
        var recording = original with { DeclaredForklift = false, Frames = frames };
        using var replay = new ReplaySession(recording);
        replay.AdvanceTo(replay.Duration);
        Assert.IsTrue(replay.Analysis.Steps.IsForkliftDeclared);
        Assert.AreEqual(0d, replay.Compare().DistanceErrorMeters!.Value, 0.01);
        replay.Seek(replay.Duration);
        Assert.AreEqual(0d, replay.Compare().DistanceErrorMeters!.Value, 0.01);
    }
    [TestMethod]
    public async Task TestThat_calculation_usage_selects_one_native_source_and_prefers_linear_acceleration()
    {
        var fixture = SyntheticScenarios.Create(SyntheticScenario.StraightWalking, 5);
        var accel = fixture.Catalogue.First() with { Id = "primary-accelerometer", Kind = SensorKind.Accelerometer };
        var linear = fixture.Catalogue.First(x => x.Kind == SensorKind.LinearAcceleration);
        var second = accel with { Id = "secondary-accelerometer" };
        await using var input = new SensorInputStream(fixture.Catalogue);
        using var steps = new StepSession(input);
        steps.Configure([accel, second]);
        Assert.IsTrue(steps.UsesSensorForPositioning(accel.Id));
        Assert.IsFalse(steps.UsesSensorForPositioning(second.Id));
        steps.Configure([accel, linear]);
        Assert.IsFalse(steps.UsesSensorForPositioning(accel.Id));
        Assert.IsTrue(steps.UsesSensorForPositioning(linear.Id));
        steps.Configure([]);
        Assert.IsFalse(steps.UsesSensorForPositioning(linear.Id));
    }
    private static TelemetryRegistration Register(string device, SensorRecording recording) => new(new(device, ClientPlatform.Android, new()), Guid.NewGuid(), TelemetryMode.Research, recording.Catalogue,
        Enumerable.Range(1,18).Select(n => new IterationInfo(n,"test","test",IterationStatus.NotStarted,"Unknown",null,null,"NotRun",[])).ToArray(),
        new(recording.DeclaredForklift, recording.DeclaredCarrying, recording.KnownStartHeading, recording.StartPosition, recording.GpsOrigin));
    [TestMethod]
    public async Task TestThat_parallel_server_calculations_do_not_mix_walking_and_truck_sessions()
    {
        using var registry = new TelemetryRegistry();
        var items = Enumerable.Range(0,8).Select(i => { var recording = SyntheticScenarios.Create(i % 2 == 0 ? SyntheticScenario.StraightWalking : SyntheticScenario.ForkliftVibration, 12); return (Recording: recording, Registration: Register($"isolated-{i}",recording)); }).ToArray();
        foreach (var item in items) registry.Register(item.Registration.Client.Id, item.Registration);
        await Task.WhenAll(items.Select(item => Task.Run(() =>
        {
            long sequence = 0;
            foreach (var chunk in item.Recording.Frames.Chunk(256))
            {
                var batch = new TelemetryBatch(item.Registration.SessionId,sequence++,DateTimeOffset.UtcNow,chunk.Select(x=>x.Event).ToArray(),7);
                registry.Apply(item.Registration.Client.Id,batch); registry.Apply(item.Registration.Client.Id,batch);
            }
        })));
        foreach (var item in items)
        {
            var result = registry.GetAnalysis(item.Registration.SessionId)!;
            if (item.Recording.DeclaredForklift) Assert.AreEqual(0L,result.Steps.Total);
            else Assert.IsGreaterThan(10L,result.Steps.Total);
            Assert.AreEqual(7L,registry.GetSessions().Single(x=>x.SessionId==item.Registration.SessionId).Statistics.DroppedEvents);
            registry.Disconnect(item.Registration.Client.Id,item.Registration.SessionId);
            Assert.IsNull(registry.GetAnalysis(item.Registration.SessionId)!.Position.SpeedMetersPerSecond);
        }
    }
    [TestMethod]
    public void TestThat_invalid_union_kind_and_context_are_rejected_before_any_partial_application()
    {
        var recording = SyntheticScenarios.Create(SyntheticScenario.Stationary,5); using var registry = new TelemetryRegistry(); var registration = Register("validation",recording); registry.Register("validation",registration);
        var valid = recording.Frames[0].Event;
        var invalid = valid with { Reading = valid.Reading! with { Kind = SensorKind.Light } };
        Assert.ThrowsExactly<InvalidDataException>(()=>registry.Apply("validation",new(registration.SessionId,0,DateTimeOffset.UtcNow,[valid,invalid])));
        Assert.AreEqual(0L,registry.GetDiagnostics(registration.SessionId).Sensors.Sum(x=>x.Count));
        Assert.ThrowsExactly<InvalidDataException>(()=>registry.Apply("validation",new(registration.SessionId,0,DateTimeOffset.UtcNow,[valid with { Context = new(KnownStartHeading:0) }])));
        Assert.ThrowsExactly<InvalidDataException>(()=>SensorEventValidation.ValidateContext(new(KnownStartHeading: double.NaN)));
    }
    [TestMethod]
    public async Task TestThat_recording_captures_operator_context_and_blocks_update_installation()
    {
        var fixture = SyntheticScenarios.Create(SyntheticScenario.Stationary,5); var provider = new SensorInputStream(fixture.Catalogue); var bus = new NativeObservationBus(); using var steps = new StepSession(provider); steps.Configure(fixture.Catalogue);
        using var vehicle = new VehicleSession(bus,steps,steps); using var radio = new RadioPositionSession(bus,steps); var guard = new UpdateSessionGuard(); using var recorder = new RecordingSession(provider,bus,steps,guard,vehicle,radio);
        await recorder.StartAsync("test capture"); Assert.ThrowsExactly<InvalidOperationException>(()=>guard.AcquireInstallation());
        provider.Emit(fixture.Frames[0].Event.Reading!); steps.SetKnownStartHeading(.5); steps.SetDeclaredForklift(true); vehicle.SetGpsOrigin(59,18);
        var recording = recorder.Stop(); Assert.IsFalse(recording.Synthetic); Assert.HasCount(4,recording.Frames); Assert.HasCount(3,recording.Frames.Where(x=>x.Event.Context is not null).ToArray());
        RecordingCodec.Validate(recording); using var lease = guard.AcquireInstallation();
    }
    [TestMethod]
    public async Task TestThat_large_radio_capture_is_truncated_and_can_be_round_tripped()
    {
        var fixture = SyntheticScenarios.Create(SyntheticScenario.Stationary,5); var provider = new SensorInputStream(fixture.Catalogue); var bus = new NativeObservationBus(); using var steps = new StepSession(provider); using var vehicle = new VehicleSession(bus,steps,steps); using var radio = new RadioPositionSession(bus,steps); var guard = new UpdateSessionGuard(); using var recorder = new RecordingSession(provider,bus,steps,guard,vehicle,radio);
        await recorder.StartAsync("bounded"); var observations = Enumerable.Range(0,512).Select(i=>new BluetoothObservation(i.ToString(),-50,new string('A',4096),DateTimeOffset.UtcNow)).ToArray();
        for(var i=0;i<5;i++) bus.Publish(observations);
        var recording = recorder.Stop(); Assert.IsTrue(recording.Truncated); Assert.IsFalse(recorder.IsRecording); Assert.IsFalse(guard.IsSessionActive);
        Assert.HasCount(recording.Frames.Count,RecordingCodec.FromCsv(RecordingCodec.ToCsv(recording)).Frames);
    }
    [TestMethod]
    public void TestThat_lost_particles_do_not_reinitialize_across_a_wall()
    {
        var filter = new Sensor3.MapMatching.IndoorParticleFilter(seed:5); var map = new IndoorMap([new(MapFeatureKind.Wall,new(0,-100),new(0,100),"wall")]);
        Assert.IsNotNull(filter.Update(new(-5,0),.2,map).Point); Assert.IsNull(filter.Update(new(5,0),.2,map).Point); Assert.IsNull(filter.Update(new(6,0),.2,map).Point);
    }
}
