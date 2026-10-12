using System.Security.Cryptography;
using System.Text;
using Sensor3.Contracts;
using Sensor3.Infrastructure;
namespace Sensor3.Tests;
[TestClass]
public sealed class DevicePairingTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [TestMethod]
    public void TestThat_pairing_is_single_use_and_only_hashes_persist_after_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), "Sensor3-pairing-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DevicePairingStore(path, TimeProvider.System);
            var invitation = store.Create();
            var device = Guid.NewGuid().ToString("N");
            var token = store.Redeem(invitation.Code, device);
            Assert.IsNotNull(token);
            Assert.IsNull(store.Redeem(invitation.Code, Guid.NewGuid().ToString("N")));
            var restarted = new DevicePairingStore(path, TimeProvider.System);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), restarted.GetHash(device));
            var persisted = File.ReadAllText(Path.Combine(path, "device-hashes.json"));
            Assert.IsFalse(persisted.Contains(token, StringComparison.Ordinal));
            Assert.IsFalse(persisted.Contains(invitation.Code, StringComparison.Ordinal));
            Assert.IsNull(store.Redeem(store.Create().Code, device));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [TestMethod]
    public void TestThat_expired_wrong_and_invalid_device_codes_do_not_register()
    {
        var path = Path.Combine(Path.GetTempPath(), "Sensor3-pairing-tests-" + Guid.NewGuid().ToString("N"));
        var clock = new Clock();
        var store = new DevicePairingStore(path, clock);
        var invite = store.Create();
        Assert.IsNull(store.Redeem(new string('0', 64), Guid.NewGuid().ToString("N")));
        Assert.IsNull(store.Redeem(invite.Code, "../device"));
        clock.Now = clock.Now.AddMinutes(5);
        Assert.IsNull(store.Redeem(invite.Code, Guid.NewGuid().ToString("N")));
        Assert.IsFalse(Directory.Exists(path));
    }
    [TestMethod]
    public async Task TestThat_concurrent_redemption_registers_only_one_device()
    {
        var path = Path.Combine(Path.GetTempPath(), "Sensor3-pairing-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DevicePairingStore(path, TimeProvider.System);
            var code = store.Create().Code;
            var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => store.Redeem(code, Guid.NewGuid().ToString("N")))));
            Assert.AreEqual(1, results.Count(x => x is not null));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [TestMethod]
    public void TestThat_https_qr_and_android_links_resolve_to_same_registration()
    {
        var code = new string('A', 64);
        var qr = PairingLink.Parse("https://prototyp.qsys.se/pair#" + code);
        var native = PairingLink.Parse("sensor3://pair?server=https%3A%2F%2Fprototyp.qsys.se&code=" + code);
        Assert.AreEqual(qr, native);
    }
    [TestMethod]
    [DataRow("http://prototyp.qsys.se/pair#AAAA", DisplayName = "HTTP rejected")]
    [DataRow("https://prototyp.qsys.se/pair#AAAA", DisplayName = "Short code rejected")]
    [DataRow("sensor3://pair?server=http%3A%2F%2Fprototyp.qsys.se&code=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", DisplayName = "Native HTTP rejected")]
    public void TestThat_invalid_links_are_rejected(string link) => Assert.ThrowsExactly<ArgumentException>(() => PairingLink.Parse(link));
}
