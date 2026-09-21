using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using Npgsql;

// Dedicated, disposable fixture tenants. Credentials are supplied by the test runner.
var connection = Environment.GetEnvironmentVariable("EQUIPMENT_TEST_CONNECTION") ?? throw new InvalidOperationException("Set EQUIPMENT_TEST_CONNECTION to a local aurora_app connection.");
var builder = new NpgsqlConnectionStringBuilder(connection);
if (builder.Host is not ("localhost" or "127.0.0.1")) throw new InvalidOperationException("These checks require a local database.");
await using var dataSource = NpgsqlDataSource.Create(connection);
var context = new TestTenant(Guid.NewGuid());
var factory = new NpgsqlConnectionFactory(dataSource, context);
var store = new EquipmentStore(factory, context);
await using var db = await factory.OpenConnectionAsync();
await db.ExecuteAsync("INSERT INTO tenant(id,name,slug) VALUES (@Id,'Equipment store test',@Slug)", new { Id = context.TenantIdOrNull, Slug = "equipment-test-" + context.TenantIdOrNull });
var count = 0;
void Check(bool success, string name) { if (!success) throw new Exception(name); Console.WriteLine("PASS: " + name); count++; }
try
{
    var type = new EquipmentTypeDto { Code = "BOX", Description = "Test box", Weight = 12000, Cubes = 1683 };
    await store.Save(type, default);
    await store.Save(new EquipmentUnitDto { Id = "UNIT1", TypeCode = "BOX", Terminal = "LAX" }, default);
    var saved = await store.List(default);
    Check(saved.Types.Single().Cubes == 1683 && saved.Units.Single().Terminal == "LAX", "equipment persists and reloads through the tenant connection");
    type.Description = "Updated box"; await store.Save(type, default);
    await store.AddMissing(new([new EquipmentTypeDto { Code = "BOX", Description = "Do not overwrite" }], [new EquipmentUnitDto { Id = "UNIT1", TypeCode = "BOX", Terminal = "OTHER" }]), default);
    saved = await store.List(default);
    Check(saved.Types.Single().Description == "Updated box" && saved.Units.Single().Terminal == "LAX", "sample import preserves existing equipment");
    try { await store.AddMissing(new([new EquipmentTypeDto { Code = "ROLLBACK", Description = "Atomic import" }], [new EquipmentUnitDto { Id = "BAD", TypeCode = "MISSING", Terminal = "LAX" }]), default); throw new Exception("Expected import rejection"); }
    catch (FormatException) { }
    Check((await store.List(default)).Types.All(x => x.Code != "ROLLBACK"), "invalid import rolls back all inserted types");
    var other = new TestTenant(Guid.NewGuid());
    var otherStore = new EquipmentStore(new NpgsqlConnectionFactory(dataSource, other), other);
    Check((await otherStore.List(default)).Types.Count == 0, "another tenant cannot read saved equipment through the store");
    await store.Save(new EquipmentUnitDto { Id = "UNIT1", TypeCode = "BOX", Terminal = "LAX", Available = false }, default);
    Check(!(await store.List(default)).Units.Single().Available, "unit availability edits persist");
    Console.WriteLine($"{count} equipment database checks passed.");
}
finally { await db.ExecuteAsync("DELETE FROM tenant WHERE id=@Id", new { Id = context.TenantIdOrNull }); }

sealed record TestTenant(Guid Id) : ITenantContext
{
    public Guid? TenantIdOrNull => Id;
    public Guid? UserIdOrNull => null;
}
