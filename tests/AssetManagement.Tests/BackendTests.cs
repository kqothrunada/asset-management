using AssetManagement.Api.Security;
using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Tests
{
    public class BackendTests : IDisposable
    {
        private readonly TestEnv e = new();
        public void Dispose() => e.Dispose();
        private static readonly CancellationToken Ct = default;

        private static AssetUpsertDto Dto(AssetCategory c = AssetCategory.ITEquipment,
            AssetSubcategory s = AssetSubcategory.Laptop, string? desc = "<p>ok</p>",
            AssetStatus st = AssetStatus.New) =>
            new() { Name = "Aset", Description = desc, Category = c, Subcategory = s, Status = st };

        [Fact]
        public async Task Asset_numbers_are_sequential_with_prefix()
        {
            var leo = e.Principal(e.Leo, "Manager");
            var a = await e.Assets().CreateAsync(Dto(), leo, Ct);
            var b = await e.Assets().CreateAsync(Dto(), leo, Ct);
            a.AssetCode.Should().Be("AST-001");
            b.AssetCode.Should().Be("AST-002");
        }

        [Fact]
        public async Task New_asset_requires_description()
        {
            var act = () => e.Assets().CreateAsync(Dto(desc: "<p></p>"), e.Principal(e.Leo, "Manager"), Ct);
            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task Maintenance_changes_recalculate_asset_value()
        {
            var leo = e.Principal(e.Leo, "Manager");
            var asset = await e.Assets().CreateAsync(Dto(), leo, Ct);
            var ms = e.Maint();

            var r1 = await ms.AddAsync(asset.Id, new() { Cost = 100m, Type = MaintenanceType.Preventive, Date = new(2026, 1, 1) }, leo, Ct);
            var r2 = await ms.AddAsync(asset.Id, new() { Cost = 250.5m, Type = MaintenanceType.Corrective, Date = new(2026, 2, 1) }, leo, Ct);
            r2.AssetValueTotal.Should().Be(350.5m);

            var r3 = await ms.UpdateAsync(asset.Id, r1.Maintenance!.Id, new() { Cost = 50m, Type = MaintenanceType.Preventive, Date = new(2026, 1, 1) }, leo, Ct);
            r3.AssetValueTotal.Should().Be(300.5m);

            var r4 = await ms.DeleteAsync(asset.Id, r2.Maintenance!.Id, leo, Ct);
            r4.AssetValueTotal.Should().Be(50m);

            (await e.Db.Assets.AsNoTracking().SingleAsync()).AssetValueTotal.Should().Be(50m);
        }

        [Fact]
        public async Task Furniture_has_no_maintenance()
        {
            var leo = e.Principal(e.Leo, "Manager");
            var chair = await e.Assets().CreateAsync(Dto(AssetCategory.Furniture, AssetSubcategory.Chair), leo, Ct);
            var act = () => e.Maint().AddAsync(chair.Id, new() { Cost = 1, Type = MaintenanceType.Preventive, Date = new(2026, 1, 1) }, leo, Ct);
            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task Record_visibility_follows_rules()
        {
            var svc = e.Assets();
            await svc.CreateAsync(Dto(), e.Principal(e.Leo, "Manager"), Ct);                       // aset buatan IT

            (await svc.Visible(e.Principal(e.Leo, "Manager")).AnyAsync()).Should().BeTrue();       // creator
            (await svc.Visible(e.Principal(e.Jane, "User")).AnyAsync()).Should().BeTrue();         // staf IT
            (await svc.Visible(e.Principal(e.Bob, "User")).AnyAsync()).Should().BeFalse();         // divisi lain
            (await svc.Visible(e.Principal(e.Sarah, "Manager", "CanViewAllAssets")).AnyAsync()).Should().BeTrue();

            await svc.CreateAsync(Dto(), e.Principal(e.Bob, "User"), Ct);
            (await svc.Visible(e.Principal(e.Bob, "User")).CountAsync()).Should().Be(1);           // hanya miliknya
        }

        [Theory]
        [InlineData("leo", "Manager", true)]     // IT Manager: full
        [InlineData("jane", "User", false)]      // IT User: tidak boleh delete
        public async Task Delete_permission_follows_role_matrix(string who, string role, bool expected)
        {
            var user = who == "leo" ? e.Leo : e.Jane;
            var ctx = new AuthorizationHandlerContext(
                new[] { new MenuPermissionRequirement("assets", MenuAction.Delete) }, e.Principal(user, role), null);
            await new MenuPermissionHandler(e.Db).HandleAsync(ctx);
            ctx.HasSucceeded.Should().Be(expected);
        }

        [Fact]
        public async Task Both_approvals_assign_the_asset()
        {
            var id = await StartReviewAsync();
            var list = await e.Db.ApprovalRequests.Where(a => a.AssetId == id).ToListAsync();
            list.Should().HaveCount(2);

            var svc = e.Approvals();
            await svc.DecideAsync(list.Single(a => a.Role == "Department Head").Id, true, null, e.Leo.Id, Ct);
            (await e.Db.Assets.AsNoTracking().SingleAsync()).Status.Should().Be(AssetStatus.UnderReview);
            await svc.DecideAsync(list.Single(a => a.Role == "Asset Manager").Id, true, null, e.Sarah.Id, Ct);
            (await e.Db.Assets.AsNoTracking().SingleAsync()).Status.Should().Be(AssetStatus.Assigned);
        }

        [Fact]
        public async Task One_rejection_discards_the_asset()
        {
            var id = await StartReviewAsync();
            var head = await e.Db.ApprovalRequests.SingleAsync(a => a.AssetId == id && a.Role == "Department Head");
            await e.Approvals().DecideAsync(head.Id, false, "Tidak sesuai", e.Leo.Id, Ct);
            (await e.Db.Assets.AsNoTracking().SingleAsync()).Status.Should().Be(AssetStatus.Discarded);
        }

        [Fact]
        public async Task Only_the_assigned_approver_can_decide()
        {
            var id = await StartReviewAsync();
            var head = await e.Db.ApprovalRequests.SingleAsync(a => a.AssetId == id && a.Role == "Department Head");
            var act = () => e.Approvals().DecideAsync(head.Id, true, null, e.Bob.Id, Ct);
            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Theory]
        [InlineData("sarah", true)]    // HQ Manager
        [InlineData("leo", false)]     // Manager, tetapi bukan HQ
        [InlineData("mike", false)]    // HQ, tetapi bukan Manager
        public async Task Export_is_for_headquarters_managers_only(string who, bool expected)
        {
            var user = who switch { "sarah" => e.Sarah, "leo" => e.Leo, _ => e.Mike };
            var ctx = new AuthorizationHandlerContext(new[] { new HQManagerRequirement() }, e.Principal(user), null);
            await new HQManagerHandler().HandleAsync(ctx);
            ctx.HasSucceeded.Should().Be(expected);
        }

        private async Task<int> StartReviewAsync()
        {
            var jane = e.Principal(e.Jane, "User");
            var created = await e.Assets().CreateAsync(Dto(), jane, Ct);
            await e.Assets().UpdateAsync(created.Id, Dto(st: AssetStatus.UnderReview), jane, Ct);
            return created.Id;
        }
    }
}
