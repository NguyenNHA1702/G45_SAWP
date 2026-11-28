using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

public class LunchMenuServiceTests : TestBase
{
    private readonly Mock<ISchoolClock> _clockMock;
    private readonly LunchMenuService _menuService;
    private readonly LunchService _lunchService;

    public LunchMenuServiceTests()
    {
        _clockMock = new Mock<ISchoolClock>();
        _clockMock.Setup(c => c.Today).Returns(new DateTime(2026, 11, 2)); // Thứ 2
        _clockMock.Setup(c => c.Now).Returns(new DateTime(2026, 11, 2, 7, 30, 0));
        _clockMock.Setup(c => c.ToSchoolTime(It.IsAny<DateTime>())).Returns((DateTime dt) => dt);

        var options = Options.Create(new SchoolOptions { LunchCutoffTime = "08:15" });
        _menuService = new LunchMenuService(UnitOfWorkMock.Object, _clockMock.Object, options, AuditServiceMock.Object);
        _lunchService = new LunchService(UnitOfWorkMock.Object, _clockMock.Object, options, AuditServiceMock.Object);
    }

    [Fact]
    public async Task CreateDish_DuplicateName_ReturnsFail()
    {
        SetupRepository(new List<Dish>
        {
            new() { Id = 1, Name = "Cá basa sốt cà chua", Category = DishCategory.MainDish, IsActive = true }
        });

        var result = await _menuService.CreateDishAsync(10, new UpsertDishRequestDto
        {
            Name = "cá basa sốt cà chua",
            Category = DishCategory.MainDish,
            SuitableMealTypes = new List<MealType> { MealType.StandardLunch },
            AllergenTags = new List<string> { "seafood" }
        });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task DeleteDish_WhenUsedInServiceDay_SoftDeactivatesInsteadOfHardDelete()
    {
        var dishes = new List<Dish>
        {
            new() { Id = 5, Name = "Canh bí đỏ nấu tôm", Category = DishCategory.Soup, IsActive = true }
        };
        var dishRepo = SetupRepository(dishes);
        SetupRepository(new List<WeeklyMenuTemplateItem>());
        SetupRepository(new List<LunchServiceDayDish>
        {
            new() { Id = 1, LunchServiceDayId = 1, MealType = MealType.StandardLunch, DishId = 5 }
        });

        var result = await _menuService.DeleteDishAsync(5, 10);

        result.Success.Should().BeTrue();
        dishes[0].IsActive.Should().BeFalse();
        dishRepo.Verify(r => r.DeleteAsync(It.IsAny<Dish>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDish_WhenUnused_HardDeletes()
    {
        var dishes = new List<Dish>
        {
            new() { Id = 6, Name = "Món mới thử nghiệm", Category = DishCategory.MainDish, IsActive = true }
        };
        var dishRepo = SetupRepository(dishes);
        SetupRepository(new List<WeeklyMenuTemplateItem>());
        SetupRepository(new List<LunchServiceDayDish>());

        var result = await _menuService.DeleteDishAsync(6, 10);

        result.Success.Should().BeTrue();
        dishRepo.Verify(r => r.DeleteAsync(It.Is<Dish>(d => d.Id == 6), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyTemplate_RespectsSchoolCalendarMakeupSaturdayAndSkipsLockedOrHoliday()
    {
        // Thứ 2 (2026-11-02) -> Thứ 7 (2026-11-07)
        // Thứ 3 (2026-11-03) là ngày đã khóa (IsLocked = true)
        // Thứ 4 (2026-11-04) nghỉ lễ theo SchoolCalendarDay (IsSchoolDay = false)
        // Thứ 7 (2026-11-07) học bù theo SchoolCalendarDay (IsSchoolDay = true)
        var dish1 = new Dish { Id = 1, Name = "Cơm trắng dẻo", Category = DishCategory.Starch, IsActive = true };
        var dish2 = new Dish { Id = 2, Name = "Gà kho nấm", Category = DishCategory.MainDish, IsActive = true };

        var template = new WeeklyMenuTemplate
        {
            Id = 10,
            Name = "Mẫu Tuần Chuẩn",
            IsActive = true,
            Items = new List<WeeklyMenuTemplateItem>
            {
                new() { Id = 1, TemplateId = 10, DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1, SortOrder = 1 },
                new() { Id = 2, TemplateId = 10, DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 2, Dish = dish2, SortOrder = 2 },
                new() { Id = 3, TemplateId = 10, DayOfWeek = 2, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1, SortOrder = 1 },
                new() { Id = 4, TemplateId = 10, DayOfWeek = 4, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1, SortOrder = 1 },
                new() { Id = 5, TemplateId = 10, DayOfWeek = 5, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1, SortOrder = 1 },
            }
        };

        SetupRepository(new List<WeeklyMenuTemplate> { template });
        SetupRepository(new List<SchoolCalendarDay>
        {
            new() { Id = 1, Date = new DateTime(2026, 11, 4), IsSchoolDay = false, Note = "Nghỉ lễ trường" },
            new() { Id = 2, Date = new DateTime(2026, 11, 7), IsSchoolDay = true, Note = "Học bù T7" }
        });

        var lockedTuesday = new LunchServiceDay
        {
            Id = 100,
            Date = new DateTime(2026, 11, 3),
            IsServing = true,
            IsLocked = true,
            Remarks = "Đã chốt trước"
        };
        var serviceDays = new List<LunchServiceDay> { lockedTuesday };
        SetupRepository(serviceDays);
        SetupRepository(new List<LunchServiceDayDish>());

        var result = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto
        {
            FromDate = new DateTime(2026, 11, 2),
            ToDate = new DateTime(2026, 11, 7),
            CutoffTime = "08:15",
            OverwriteExistingMenus = true
        });

        result.Success.Should().BeTrue();
        result.Data!.AppliedDates.Should().Contain(new[]
        {
            new DateTime(2026, 11, 2),
            new DateTime(2026, 11, 5),
            new DateTime(2026, 11, 6),
            new DateTime(2026, 11, 7), // Thứ 7 học bù mượn thực đơn Thứ 2 của mẫu
        });
        result.Data.SkippedLockedDates.Should().Contain(new DateTime(2026, 11, 3));
        result.Data.SkippedNonSchoolDates.Should().Contain(new DateTime(2026, 11, 4));
        lockedTuesday.Remarks.Should().Be("Đã chốt trước");

        var mondayServiceDay = serviceDays.First(d => d.Date == new DateTime(2026, 11, 2));
        mondayServiceDay.Remarks.Should().Be("Cơm trắng dẻo, Gà kho nấm");
    }

    [Fact]
    public void DetectConflicts_MatchesAllergenTagsAndIgnoresSafeDishes()
    {
        var seafoodDish = new Dish { Id = 1, Name = "Canh bí đỏ nấu tôm", AllergenTags = "seafood" };
        var safeDish = new Dish { Id = 2, Name = "Thịt nạc hấp mềm", AllergenTags = null };

        var conflictsWithSeafood = AllergenCatalog.DetectConflicts(
            new[] { "seafood" },
            null,
            new[] { seafoodDish, safeDish });
        conflictsWithSeafood.Should().HaveCount(1);
        conflictsWithSeafood[0].Should().Contain("Canh bí đỏ nấu tôm");

        var conflictsWhenSwitchedToSpecial = AllergenCatalog.DetectConflicts(
            new[] { "seafood" },
            null,
            new[] { safeDish });
        conflictsWhenSwitchedToSpecial.Should().BeEmpty();
    }

    [Fact]
    public async Task StudentMealProfile_ParentOfAnotherStudent_IsForbidden()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<Guardian>
        {
            new()
            {
                Id = 1,
                UserId = 50,
                StudentGuardians = new List<StudentGuardian> { new() { StudentId = 2 } } // Con khác, không phải Id=1
            }
        });
        SetupRepository(new List<StudentMealProfile>());

        var result = await _menuService.UpsertStudentMealProfileAsync(
            1,
            50,
            new[] { UserRoles.Parent },
            new UpsertStudentMealProfileRequestDto
            {
                DefaultMealType = MealType.VegetarianLunch,
                AllergenTags = new List<string> { "seafood" }
            });

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task StudentMealProfile_ParentUpdatesOwnChild_AndBoardDetectsAllergyConflict()
    {
        var targetDate = new DateTime(2026, 11, 2);
        var student = new Student
        {
            Id = 1,
            StudentCode = "HS001",
            FullName = "Bé An",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment>
            {
                new() { Id = 1, StudentId = 1, ClassId = 10, IsActive = true, StartDate = new DateTime(2026, 9, 1), Class = new Class { Id = 10, ClassName = "1A", Grade = 1, Status = "Active" } }
            }
        };

        SetupRepository(new List<Student> { student });
        SetupRepository(new List<Guardian>
        {
            new()
            {
                Id = 1,
                UserId = 50,
                StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1 } }
            }
        });
        var profiles = new List<StudentMealProfile>();
        SetupRepository(profiles);

        var saveRes = await _menuService.UpsertStudentMealProfileAsync(
            1,
            50,
            new[] { UserRoles.Parent },
            new UpsertStudentMealProfileRequestDto
            {
                DefaultMealType = MealType.SpecialDiet,
                AllergenTags = new List<string> { "seafood" },
                AllergyNotes = "Dị ứng nặng hải sản"
            });

        saveRes.Success.Should().BeTrue();
        saveRes.Data!.AllergenTags.Should().ContainSingle("seafood");
        saveRes.Data.DefaultMealType.Should().Be(MealType.SpecialDiet); // Phụ huynh được khai báo chế độ ăn & dị ứng trong đơn đăng ký bán trú

        // Setup board với món có hải sản trong thực đơn StandardLunch ngày 2026-11-02
        var serviceDay = new LunchServiceDay { Id = 7, Date = targetDate, IsServing = true, CutoffTime = new TimeSpan(8, 15, 0) };
        SetupRepository(new List<LunchServiceDay> { serviceDay });
        SetupRepository(new List<LunchServiceDayDish>
        {
            new()
            {
                Id = 1,
                LunchServiceDayId = 7,
                LunchServiceDay = serviceDay,
                MealType = MealType.StandardLunch,
                DishId = 99,
                Dish = new Dish { Id = 99, Name = "Canh cua mồng tơi", AllergenTags = "seafood" },
                SortOrder = 1
            }
        });
        SetupRepository(new List<LunchRegistration>
        {
            new()
            {
                Id = 200,
                StudentId = 1,
                Student = student,
                Date = targetDate,
                MealType = MealType.StandardLunch,
                IsActive = true
            }
        });
        SetupRepository(new List<SchoolYear> { new() { Id = 1, IsActive = true } });
        SetupRepository(new List<Class> { new() { Id = 10, ClassName = "1A", Grade = 1, SchoolYearId = 1, Status = "Active" } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AuditLog>());

        var boardRes = await _lunchService.GetDailyMealBoardAsync(targetDate, 99, new[] { UserRoles.ServiceStaff });

        boardRes.Success.Should().BeTrue();
        boardRes.Data!.Menus.Should().NotBeNull();
        boardRes.Data.Menus!.Standard.Should().ContainSingle(d => d.Name == "Canh cua mồng tơi");
        boardRes.Data.StudentMeals.Should().ContainSingle();
        boardRes.Data.StudentMeals[0].AllergyConflicts.Should().ContainSingle(c => c.Contains("Canh cua mồng tơi"));
    }

    [Fact]
    public async Task UpsertServiceDays_WithMenuTemplateId_AppliesAllMealTypesForMatchingWeekday()
    {
        var thursday = new DateTime(2026, 11, 26); // Thứ 5 => DayOfWeek = 4
        var stdDish = new Dish { Id = 1, Name = "Trứng đúc thịt hấp", Category = DishCategory.MainDish, IsActive = true };
        var vegDish = new Dish { Id = 2, Name = "Đậu phụ sốt cà chua", Category = DishCategory.MainDish, IsActive = true };
        var spcDish = new Dish { Id = 3, Name = "Cháo thịt nạc mềm", Category = DishCategory.Starch, IsActive = true };

        var template = new WeeklyMenuTemplate
        {
            Id = 20,
            Name = "Thực đơn Tuần A",
            IsActive = true,
            Items = new List<WeeklyMenuTemplateItem>
            {
                new() { Id = 1, TemplateId = 20, DayOfWeek = 4, MealType = MealType.StandardLunch, DishId = 1, Dish = stdDish, SortOrder = 1 },
                new() { Id = 2, TemplateId = 20, DayOfWeek = 4, MealType = MealType.VegetarianLunch, DishId = 2, Dish = vegDish, SortOrder = 1 },
                new() { Id = 3, TemplateId = 20, DayOfWeek = 4, MealType = MealType.SpecialDiet, DishId = 3, Dish = spcDish, SortOrder = 1 },
            }
        };

        SetupRepository(new List<WeeklyMenuTemplate> { template });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchServiceDayDish>());

        var res = await _lunchService.UpsertServiceDaysAsync(99, new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { thursday },
            IsServing = true,
            CutoffTime = "08:15",
            MenuTemplateId = 20
        });

        res.Success.Should().BeTrue();
        res.Data.Should().ContainSingle();
        res.Data![0].Remarks.Should().Be("Trứng đúc thịt hấp");
        res.Data[0].Menus.Should().NotBeNull();
        res.Data[0].Menus!.Standard.Should().ContainSingle(d => d.Name == "Trứng đúc thịt hấp");
        res.Data[0].Menus!.Vegetarian.Should().ContainSingle(d => d.Name == "Đậu phụ sốt cà chua");
        res.Data[0].Menus!.Special.Should().ContainSingle(d => d.Name == "Cháo thịt nạc mềm");
    }

    [Fact]
    public async Task UpsertServiceDays_ChangeCutoffWithoutTemplate_PreservesExistingDishesAndRemarks()
    {
        var thursday = new DateTime(2026, 11, 26);
        var stdDish = new Dish { Id = 1, Name = "Cá basa sốt cà chua", Category = DishCategory.MainDish, IsActive = true };
        var existingDay = new LunchServiceDay
        {
            Id = 55,
            Date = thursday,
            IsServing = true,
            CutoffTime = new TimeSpan(8, 15, 0),
            Remarks = "Cá basa sốt cà chua",
            Dishes = new List<LunchServiceDayDish>
            {
                new() { Id = 1, LunchServiceDayId = 55, MealType = MealType.StandardLunch, DishId = 1, Dish = stdDish, SortOrder = 1 }
            }
        };

        SetupRepository(new List<LunchServiceDay> { existingDay });
        SetupRepository(new List<LunchServiceDayDish>());

        var res = await _lunchService.UpsertServiceDaysAsync(99, new UpsertLunchServiceDaysRequestDto
        {
            Dates = new List<DateTime> { thursday },
            IsServing = true,
            CutoffTime = "08:30"
        });

        res.Success.Should().BeTrue();
        res.Data![0].CutoffTime.Should().Be("08:30");
        res.Data[0].Remarks.Should().Be("Cá basa sốt cà chua");
        res.Data[0].Menus!.Standard.Should().ContainSingle(d => d.Name == "Cá basa sốt cà chua");
    }

    [Fact]
    public async Task Parent_RegisterBatchCannotOverrideMealType_AndUpdateMealTypeReturnsForbidden()
    {
        var tomorrow = new DateTime(2026, 11, 3);
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé Nam" } });
        SetupRepository(new List<Guardian>
        {
            new()
            {
                Id = 1,
                UserId = 50,
                StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1 } }
            }
        });
        SetupRepository(new List<StudentMealProfile>());
        SetupRepository(new List<LunchServiceDay>());
        var regs = new List<LunchRegistration>();
        SetupRepository(regs);
        SetupRepository(new List<Notification>());

        // Phụ huynh chọn MealType = VegetarianLunch trong đơn đăng ký bán trú -> hệ thống lưu đúng chế độ ăn đã chọn
        var regRes = await _lunchService.RegisterLunchBatchAsync(
            50,
            new[] { UserRoles.Parent },
            new BatchLunchRegistrationRequestDto
            {
                StudentId = 1,
                Dates = new List<DateTime> { tomorrow },
                MealType = MealType.VegetarianLunch
            });

        regRes.Success.Should().BeTrue();
        regs.Should().ContainSingle();
        regs[0].MealType.Should().Be(MealType.VegetarianLunch);

        // Phụ huynh đổi chế độ ăn ngày chưa qua hạn chốt (UC-29 A1) -> thành công; phụ huynh không liên kết -> bị chặn MSG10
        var updRes = await _lunchService.UpdateRegistrationMealTypeAsync(
            50,
            new[] { UserRoles.Parent },
            new UpdateLunchRegistrationMealTypeDto
            {
                StudentId = 1,
                Date = tomorrow,
                MealType = MealType.StandardLunch
            });

        updRes.Success.Should().BeTrue();
        regs[0].MealType.Should().Be(MealType.StandardLunch);

        var unlinkedRes = await _lunchService.UpdateRegistrationMealTypeAsync(
            999,
            new[] { UserRoles.Parent },
            new UpdateLunchRegistrationMealTypeDto
            {
                StudentId = 1,
                Date = tomorrow,
                MealType = MealType.VegetarianLunch
            });

        unlinkedRes.Success.Should().BeFalse();
        unlinkedRes.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task ServiceStaff_CanAssignDefaultMealTypeAndUpdateDailyMealTypeBeforeLock()
    {
        var today = new DateTime(2026, 11, 2);
        _clockMock.Setup(c => c.Now).Returns(today.AddHours(9)); // Đã qua 08:15 nhưng ngày chưa khóa sổ (IsLocked = false)

        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<StudentMealProfile>());
        SetupRepository(new List<LunchServiceDay>
        {
            new() { Id = 1, Date = today, IsServing = true, CutoffTime = new TimeSpan(8, 15, 0), IsLocked = false }
        });
        var reg = new LunchRegistration { Id = 10, StudentId = 1, Date = today, MealType = MealType.StandardLunch, IsActive = true };
        SetupRepository(new List<LunchRegistration> { reg });

        var profileRes = await _menuService.UpsertStudentMealProfileAsync(
            1,
            99,
            new[] { UserRoles.ServiceStaff },
            new UpsertStudentMealProfileRequestDto
            {
                DefaultMealType = MealType.SpecialDiet,
                AllergenTags = new List<string> { "seafood" },
                AllergyNotes = "Cháo mềm kiêng hải sản"
            });

        var dailyRes = await _lunchService.UpdateRegistrationMealTypeAsync(
            99,
            new[] { UserRoles.ServiceStaff },
            new UpdateLunchRegistrationMealTypeDto
            {
                StudentId = 1,
                Date = today,
                MealType = MealType.SpecialDiet,
                Remarks = "Cháo mềm kiêng hải sản"
            });

        profileRes.Success.Should().BeTrue();
        profileRes.Data!.DefaultMealType.Should().Be(MealType.SpecialDiet);
        dailyRes.Success.Should().BeTrue();
        reg.MealType.Should().Be(MealType.SpecialDiet);
    }

    [Fact]
    public void DetectConflicts_IgnoresNonAllergyMenuRemarks_AndMatchesExplicitAllergyNote()
    {
        var tofuDish = new Dish { Id = 1, Name = "Đậu phụ sốt cà chua", AllergenTags = "soy" };
        var shrimpDish = new Dish { Id = 2, Name = "Canh bí đỏ nấu tôm", AllergenTags = "seafood" };

        // Trường hợp HS017: Remarks liệt kê món chay trước dấu '|' nhưng ghi chú chỉ là "Suất ăn chay thanh đạm" -> không phải dị ứng
        var falsePositiveCheck = AllergenCatalog.DetectConflicts(
            null,
            null,
            new[] { tofuDish },
            "Cơm trắng dẻo, Đậu phụ sốt cà chua, Nấm đùi gà kho xì dầu | Ghi chú: Suất ăn chay thanh đạm");
        falsePositiveCheck.Should().BeEmpty();

        // Trường hợp HS006: Ghi chú có chữ "Dị ứng tôm" -> bắt đúng xung đột hải sản
        var realAllergyCheck = AllergenCatalog.DetectConflicts(
            null,
            null,
            new[] { shrimpDish },
            "Cơm mềm dẻo | Ghi chú: Dị ứng tôm & hải sản có vỏ");
        realAllergyCheck.Should().ContainSingle(c => c.Contains("Canh bí đỏ nấu tôm"));
    }

    [Fact]
    public void DetectConflicts_TrustedAllergyNotesWithoutPrefix_MatchesStandardGroupAndCustomFoods()
    {
        var tofuDish = new Dish { Id = 1, Name = "Đậu phụ sốt cà chua", AllergenTags = "soy" };
        var chayoteDish = new Dish { Id = 2, Name = "Su su xào cà rốt", AllergenTags = null };
        var porkDish = new Dish { Id = 3, Name = "Thịt nạc thăn hấp mềm", AllergenTags = null };

        // Phụ huynh chỉ gõ "đậu phụ" (không gõ chữ "dị ứng" và không tích tag) -> vẫn tự bắt nhóm soy + món Đậu phụ sốt cà chua
        var directFoodOnly = AllergenCatalog.DetectConflicts(
            null,
            "đậu phụ",
            new[] { tofuDish, porkDish });
        directFoodOnly.Should().ContainSingle(c => c.Contains("Đậu phụ sốt cà chua"));

        // Phụ huynh gõ nguyên liệu ngoài 6 nhóm ("su su, cà chua") -> bắt cả Su su xào cà rốt & Đậu phụ sốt cà chua
        var customIngredients = AllergenCatalog.DetectConflicts(
            new[] { "egg" },
            "bé không ăn được su su, cà chua",
            new[] { tofuDish, chayoteDish, porkDish });
        customIngredients.Should().Contain(c => c.Contains("Su su xào cà rốt") && c.Contains("su su"));
        customIngredients.Should().Contain(c => c.Contains("Đậu phụ sốt cà chua") && c.Contains("cà chua"));
        customIngredients.Should().NotContain(c => c.Contains("Thịt nạc thăn hấp mềm"));
    }

    [Fact]
    public async Task GetDailyMealBoard_MultiAllergyStudent_ReturnsFullTagsNotesAndAccurateSwitchConflicts()
    {
        var targetDate = new DateTime(2026, 11, 26);
        var student = new Student
        {
            Id = 31,
            StudentCode = "HS031",
            FullName = "Trần Bảo Ngọc",
            Status = "Active",
            ClassAssignments = new List<ClassAssignment>
            {
                new() { Id = 1, StudentId = 31, ClassId = 10, IsActive = true, StartDate = new DateTime(2026, 9, 1), Class = new Class { Id = 10, ClassName = "1B", Grade = 1, Status = "Active" } }
            }
        };

        SetupRepository(new List<Student> { student });
        SetupRepository(new List<StudentMealProfile>
        {
            new()
            {
                Id = 1,
                StudentId = 31,
                DefaultMealType = MealType.StandardLunch,
                AllergenTags = "egg,soy",
                AllergyNotes = "đậu phụ"
            }
        });

        var eggDish = new Dish { Id = 1, Name = "Trứng đúc thịt hấp", AllergenTags = "egg" };
        var tofuDish = new Dish { Id = 2, Name = "Đậu phụ sốt cà chua", AllergenTags = "soy" };
        var safeDish = new Dish { Id = 3, Name = "Thịt nạc thăn hấp mềm", AllergenTags = null };
        var serviceDay = new LunchServiceDay { Id = 10, Date = targetDate, IsServing = true, CutoffTime = new TimeSpan(8, 15, 0) };
        SetupRepository(new List<LunchServiceDay> { serviceDay });
        SetupRepository(new List<LunchServiceDayDish>
        {
            new() { Id = 1, LunchServiceDayId = 10, LunchServiceDay = serviceDay, MealType = MealType.StandardLunch, DishId = 1, Dish = eggDish, SortOrder = 1 },
            new() { Id = 2, LunchServiceDayId = 10, LunchServiceDay = serviceDay, MealType = MealType.VegetarianLunch, DishId = 2, Dish = tofuDish, SortOrder = 1 },
            new() { Id = 3, LunchServiceDayId = 10, LunchServiceDay = serviceDay, MealType = MealType.SpecialDiet, DishId = 3, Dish = safeDish, SortOrder = 1 },
        });
        SetupRepository(new List<LunchRegistration>
        {
            new() { Id = 301, StudentId = 31, Student = student, Date = targetDate, MealType = MealType.VegetarianLunch, IsActive = true }
        });
        SetupRepository(new List<SchoolYear> { new() { Id = 1, IsActive = true } });
        SetupRepository(new List<Class> { new() { Id = 10, ClassName = "1B", Grade = 1, SchoolYearId = 1, Status = "Active" } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AuditLog>());

        var boardRes = await _lunchService.GetDailyMealBoardAsync(targetDate, 99, new[] { UserRoles.ServiceStaff });

        boardRes.Success.Should().BeTrue();
        var item = boardRes.Data!.StudentMeals.Should().ContainSingle().Subject;
        item.StudentAllergenTags.Should().BeEquivalentTo(new[] { "egg", "soy" });
        item.StudentAllergyNotes.Should().Be("đậu phụ");
        item.AllergyWarning.Should().Contain("Trứng").And.Contain("Đậu nành / Đậu phụ").And.Contain("đậu phụ");
        item.AllergyConflicts.Should().ContainSingle(c => c.Contains("Đậu phụ sốt cà chua"));
    }

    [Fact]
    public async Task UpdateRegistrationMealType_ToSafeConfiguredSpecialDiet_AllowsOptionalRemarks()
    {
        var targetDate = new DateTime(2026, 11, 26);
        var safeSpecialDish = new Dish { Id = 3, Name = "Thịt nạc thăn hấp mềm", AllergenTags = null };
        var serviceDay = new LunchServiceDay { Id = 10, Date = targetDate, IsServing = true, CutoffTime = new TimeSpan(8, 15, 0) };

        SetupRepository(new List<LunchServiceDay> { serviceDay });
        SetupRepository(new List<LunchServiceDayDish>
        {
            new() { Id = 1, LunchServiceDayId = 10, LunchServiceDay = serviceDay, MealType = MealType.SpecialDiet, DishId = 3, Dish = safeSpecialDish, SortOrder = 1 }
        });
        SetupRepository(new List<StudentMealProfile>
        {
            new() { Id = 1, StudentId = 31, DefaultMealType = MealType.StandardLunch, AllergenTags = "egg,soy", AllergyNotes = "đậu phụ" }
        });
        var reg = new LunchRegistration { Id = 301, StudentId = 31, Date = targetDate, MealType = MealType.StandardLunch, IsActive = true };
        SetupRepository(new List<LunchRegistration> { reg });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AuditLog>());

        var res = await _lunchService.UpdateRegistrationMealTypeAsync(
            99,
            new[] { UserRoles.ServiceStaff },
            new UpdateLunchRegistrationMealTypeDto
            {
                StudentId = 31,
                Date = targetDate,
                MealType = MealType.SpecialDiet,
                Remarks = null
            });

        res.Success.Should().BeTrue();
        reg.MealType.Should().Be(MealType.SpecialDiet);
    }

    // ==================== 1. GetDishesAsync (N/A/B) ====================

    [Fact]
    public async Task GetDishes_DefaultExcludesInactive_ReturnsSortedActiveDishes()
    {
        SetupRepository(new List<Dish>
        {
            new() { Id = 1, Name = "Thịt kho", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch", IsActive = true },
            new() { Id = 2, Name = "Canh bí", Category = DishCategory.Soup, SuitableMealTypes = "StandardLunch,VegetarianLunch", IsActive = true },
            new() { Id = 3, Name = "Món cũ", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch", IsActive = false }
        });

        var res = await _menuService.GetDishesAsync();

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(2);
        res.Data!.Should().NotContain(d => !d.IsActive);
        res.Data[0].Name.Should().Be("Thịt kho");
        res.Data[1].Name.Should().Be("Canh bí");
    }

    [Fact]
    public async Task GetDishes_IncludeInactiveAndFiltersByCategoryMealTypeSearch_ReturnsMatches()
    {
        SetupRepository(new List<Dish>
        {
            new() { Id = 1, Name = "Đậu phụ sốt cà", Category = DishCategory.MainDish, SuitableMealTypes = "VegetarianLunch", IsActive = false },
            new() { Id = 2, Name = "Gà kho gừng", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch", IsActive = true },
            new() { Id = 3, Name = "Canh rau ngót", Category = DishCategory.Soup, SuitableMealTypes = "VegetarianLunch", IsActive = true }
        });

        var res = await _menuService.GetDishesAsync(DishCategory.MainDish, MealType.VegetarianLunch, "  đậu phụ ", includeInactive: true);

        res.Success.Should().BeTrue();
        res.Data.Should().ContainSingle(d => d.Id == 1);
    }

    [Fact]
    public async Task GetDishes_NoMatchingSearch_ReturnsEmptyList()
    {
        SetupRepository(new List<Dish>
        {
            new() { Id = 1, Name = "Cơm trắng", Category = DishCategory.Starch, IsActive = true }
        });

        var res = await _menuService.GetDishesAsync(search: "pizza");

        res.Success.Should().BeTrue();
        res.Data.Should().BeEmpty();
    }

    // ==================== 2. CreateDishAsync (N/A/B) ====================

    [Fact]
    public async Task CreateDish_ValidInput_CreatesDishAndLogsAudit()
    {
        var dishes = new List<Dish>();
        SetupRepository(dishes);

        var res = await _menuService.CreateDishAsync(10, new UpsertDishRequestDto
        {
            Name = "  Cá hồi áp chảo  ",
            Category = DishCategory.MainDish,
            SuitableMealTypes = new List<MealType> { MealType.StandardLunch, MealType.SpecialDiet },
            AllergenTags = new List<string> { "seafood", "unknown_tag" },
            IsActive = true
        });

        res.Success.Should().BeTrue();
        res.Data!.Name.Should().Be("Cá hồi áp chảo");
        res.Data.AllergenTags.Should().ContainSingle("seafood");
        dishes.Should().ContainSingle();
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.LunchDishCreate, nameof(Dish), It.IsAny<string>(), null, It.IsAny<object?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDish_EmptyOrTooLongName_FailsWithMSG01()
    {
        SetupRepository(new List<Dish>());

        var emptyRes = await _menuService.CreateDishAsync(10, new UpsertDishRequestDto { Name = "   " });
        var longRes = await _menuService.CreateDishAsync(10, new UpsertDishRequestDto { Name = new string('A', 121) });

        emptyRes.Success.Should().BeFalse();
        emptyRes.Code.Should().Be(SystemMessages.MSG01.Code);
        longRes.Success.Should().BeFalse();
        longRes.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task CreateDish_Exact120CharNameAndEmptyMealTypes_SucceedsWithDefaultStandard()
    {
        SetupRepository(new List<Dish>());

        var res = await _menuService.CreateDishAsync(10, new UpsertDishRequestDto
        {
            Name = new string('M', 120),
            Category = DishCategory.SideDish,
            SuitableMealTypes = new List<MealType>()
        });

        res.Success.Should().BeTrue();
        res.Data!.SuitableMealTypes.Should().ContainSingle(m => m == MealType.StandardLunch);
    }

    // ==================== 3. UpdateDishAsync (N/A/B) ====================

    [Fact]
    public async Task UpdateDish_ValidInput_UpdatesFieldsAndLogsAudit()
    {
        var dish = new Dish { Id = 1, Name = "Cơm trắng", Category = DishCategory.Starch, SuitableMealTypes = "StandardLunch", IsActive = true };
        SetupRepository(new List<Dish> { dish });

        var res = await _menuService.UpdateDishAsync(1, 10, new UpsertDishRequestDto
        {
            Name = "Cơm gạo lứt",
            Category = DishCategory.Starch,
            SuitableMealTypes = new List<MealType> { MealType.VegetarianLunch },
            AllergenTags = new List<string>(),
            IsActive = true
        });

        res.Success.Should().BeTrue();
        res.Data!.Name.Should().Be("Cơm gạo lứt");
        res.Data.SuitableMealTypes.Should().ContainSingle(m => m == MealType.VegetarianLunch);
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.LunchDishUpdate, nameof(Dish), "1", It.IsAny<object?>(), It.IsAny<object?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDish_SameNameOnSameDish_Succeeds()
    {
        SetupRepository(new List<Dish> { new() { Id = 1, Name = "Cơm trắng", Category = DishCategory.Starch, IsActive = true } });

        var res = await _menuService.UpdateDishAsync(1, 10, new UpsertDishRequestDto { Name = "cơm trắng", Category = DishCategory.Starch, IsActive = false });

        res.Success.Should().BeTrue();
        res.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateDish_NotFound_FailsWithMSG11()
    {
        SetupRepository(new List<Dish>());

        var res = await _menuService.UpdateDishAsync(999, 10, new UpsertDishRequestDto { Name = "Món mới" });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UpdateDish_DuplicateNameAnotherDish_FailsWithMSG09()
    {
        SetupRepository(new List<Dish>
        {
            new() { Id = 1, Name = "Món A", IsActive = true },
            new() { Id = 2, Name = "Món B", IsActive = true }
        });

        var res = await _menuService.UpdateDishAsync(1, 10, new UpsertDishRequestDto { Name = "món b" });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UpdateDish_InvalidName_FailsWithMSG01()
    {
        SetupRepository(new List<Dish> { new() { Id = 1, Name = "Món A", IsActive = true } });

        var emptyRes = await _menuService.UpdateDishAsync(1, 10, new UpsertDishRequestDto { Name = "" });
        var longRes = await _menuService.UpdateDishAsync(1, 10, new UpsertDishRequestDto { Name = new string('Z', 121) });

        emptyRes.Success.Should().BeFalse();
        emptyRes.Code.Should().Be(SystemMessages.MSG01.Code);
        longRes.Success.Should().BeFalse();
        longRes.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 4. DeleteDishAsync (additional N/A) ====================

    [Fact]
    public async Task DeleteDish_WhenUsedInTemplate_SoftDeactivatesInsteadOfHardDelete()
    {
        var dishes = new List<Dish> { new() { Id = 7, Name = "Trứng chiên", IsActive = true } };
        var dishRepo = SetupRepository(dishes);
        SetupRepository(new List<WeeklyMenuTemplateItem> { new() { Id = 1, TemplateId = 1, DishId = 7 } });
        SetupRepository(new List<LunchServiceDayDish>());

        var res = await _menuService.DeleteDishAsync(7, 10);

        res.Success.Should().BeTrue();
        dishes[0].IsActive.Should().BeFalse();
        dishRepo.Verify(r => r.DeleteAsync(It.IsAny<Dish>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDish_NotFound_FailsWithMSG11()
    {
        SetupRepository(new List<Dish>());

        var res = await _menuService.DeleteDishAsync(999, 10);

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    // ==================== 5. GetTemplatesAsync (N/B) ====================

    [Fact]
    public async Task GetTemplates_DefaultExcludesInactive_ReturnsSortedItems()
    {
        var dish1 = new Dish { Id = 1, Name = "Cơm", Category = DishCategory.Starch };
        var dish2 = new Dish { Id = 2, Name = "Canh", Category = DishCategory.Soup };
        SetupRepository(new List<WeeklyMenuTemplate>
        {
            new()
            {
                Id = 1, Name = "Mẫu B", IsActive = true,
                Items = new List<WeeklyMenuTemplateItem>
                {
                    new() { Id = 2, DayOfWeek = 2, MealType = MealType.StandardLunch, DishId = 2, Dish = dish2, SortOrder = 2 },
                    new() { Id = 1, DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1, SortOrder = 1 }
                }
            },
            new() { Id = 2, Name = "Mẫu A (Ẩn)", IsActive = false, Items = new List<WeeklyMenuTemplateItem>() }
        });

        var res = await _menuService.GetTemplatesAsync();

        res.Success.Should().BeTrue();
        res.Data.Should().ContainSingle(t => t.Id == 1);
        res.Data![0].Items[0].DayOfWeek.Should().Be(1);
        res.Data[0].Items[1].DayOfWeek.Should().Be(2);
    }

    [Fact]
    public async Task GetTemplates_IncludeInactive_ReturnsAllTemplates()
    {
        SetupRepository(new List<WeeklyMenuTemplate>
        {
            new() { Id = 1, Name = "Mẫu A", IsActive = true, Items = new List<WeeklyMenuTemplateItem>() },
            new() { Id = 2, Name = "Mẫu B", IsActive = false, Items = new List<WeeklyMenuTemplateItem>() }
        });

        var res = await _menuService.GetTemplatesAsync(includeInactive: true);

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTemplates_EmptyRepository_ReturnsEmptyList()
    {
        SetupRepository(new List<WeeklyMenuTemplate>());

        var res = await _menuService.GetTemplatesAsync();

        res.Success.Should().BeTrue();
        res.Data.Should().BeEmpty();
    }

    // ==================== 6. CreateTemplateAsync (N/A/B) ====================

    [Fact]
    public async Task CreateTemplate_ValidInput_CreatesTemplateAndDeduplicatesItems()
    {
        var dish1 = new Dish { Id = 1, Name = "Cơm trắng", Category = DishCategory.Starch, IsActive = true };
        SetupRepository(new List<Dish> { dish1 });
        var templates = new List<WeeklyMenuTemplate>();
        SetupRepository(templates);

        var res = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "Thực đơn Tuần 1",
            Description = "Chuẩn tiểu học",
            IsActive = true,
            Items = new List<WeeklyMenuTemplateItemInputDto>
            {
                new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, SortOrder = 1 },
                new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, SortOrder = 2 },
                new() { DayOfWeek = 6, MealType = MealType.StandardLunch, DishId = 1, SortOrder = 1 }
            }
        });

        res.Success.Should().BeTrue();
        templates.Should().ContainSingle();
        templates[0].Items.Should().HaveCount(2);
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.LunchMenuTemplateCreate, nameof(WeeklyMenuTemplate), It.IsAny<string>(), null, It.IsAny<object?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplate_InvalidNameOrDescription_FailsWithMSG01()
    {
        SetupRepository(new List<WeeklyMenuTemplate>());

        var emptyName = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto { Name = " " });
        var longName = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto { Name = new string('N', 121) });
        var longDesc = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto { Name = "Mẫu hợp lệ", Description = new string('D', 251) });

        emptyName.Code.Should().Be(SystemMessages.MSG01.Code);
        longName.Code.Should().Be(SystemMessages.MSG01.Code);
        longDesc.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateName_FailsWithMSG09()
    {
        SetupRepository(new List<WeeklyMenuTemplate> { new() { Id = 1, Name = "Mẫu Tuần 1", IsActive = true } });

        var res = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto { Name = "mẫu tuần 1" });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task CreateTemplate_EmptyItemsInvalidDayOfWeekOrMissingDish_FailsWithMSG01()
    {
        SetupRepository(new List<WeeklyMenuTemplate>());
        SetupRepository(new List<Dish> { new() { Id = 1, Name = "Cơm" } });

        var emptyItems = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto { Name = "T1", Items = new() });
        var dayZero = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "T2",
            Items = new() { new() { DayOfWeek = 0, MealType = MealType.StandardLunch, DishId = 1 } }
        });
        var daySeven = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "T3",
            Items = new() { new() { DayOfWeek = 7, MealType = MealType.StandardLunch, DishId = 1 } }
        });
        var missingDish = await _menuService.CreateTemplateAsync(10, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "T4",
            Items = new() { new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 999 } }
        });

        emptyItems.Code.Should().Be(SystemMessages.MSG01.Code);
        dayZero.Code.Should().Be(SystemMessages.MSG01.Code);
        daySeven.Code.Should().Be(SystemMessages.MSG01.Code);
        missingDish.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 7. UpdateTemplateAsync (N/A) ====================

    [Fact]
    public async Task UpdateTemplate_ValidInput_ReplacesItemsAndLogsAudit()
    {
        var dish1 = new Dish { Id = 1, Name = "Cơm", Category = DishCategory.Starch };
        var dish2 = new Dish { Id = 2, Name = "Canh", Category = DishCategory.Soup };
        var oldItem = new WeeklyMenuTemplateItem { Id = 1, TemplateId = 10, DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, Dish = dish1 };
        var template = new WeeklyMenuTemplate { Id = 10, Name = "Mẫu cũ", IsActive = true, Items = new List<WeeklyMenuTemplateItem> { oldItem } };
        SetupRepository(new List<WeeklyMenuTemplate> { template });
        SetupRepository(new List<WeeklyMenuTemplateItem> { oldItem });
        SetupRepository(new List<Dish> { dish1, dish2 });

        var res = await _menuService.UpdateTemplateAsync(10, 99, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "Mẫu mới",
            Description = "Đã cập nhật",
            IsActive = true,
            Items = new List<WeeklyMenuTemplateItemInputDto>
            {
                new() { DayOfWeek = 2, MealType = MealType.VegetarianLunch, DishId = 2, SortOrder = 1 }
            }
        });

        res.Success.Should().BeTrue();
        template.Name.Should().Be("Mẫu mới");
        template.Items.Should().ContainSingle(i => i.DishId == 2 && i.DayOfWeek == 2);
        AuditServiceMock.Verify(a => a.LogActionAsync(99, AuditActions.LunchMenuTemplateUpdate, nameof(WeeklyMenuTemplate), "10", It.IsAny<object?>(), It.IsAny<object?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_NotFound_FailsWithMSG11()
    {
        SetupRepository(new List<WeeklyMenuTemplate>());

        var res = await _menuService.UpdateTemplateAsync(999, 10, new UpsertWeeklyMenuTemplateRequestDto { Name = "Mẫu" });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UpdateTemplate_DuplicateNameAnotherTemplate_FailsWithMSG09()
    {
        SetupRepository(new List<WeeklyMenuTemplate>
        {
            new() { Id = 1, Name = "Mẫu 1", Items = new List<WeeklyMenuTemplateItem>() },
            new() { Id = 2, Name = "Mẫu 2", Items = new List<WeeklyMenuTemplateItem>() }
        });

        var res = await _menuService.UpdateTemplateAsync(1, 10, new UpsertWeeklyMenuTemplateRequestDto { Name = "mẫu 2" });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UpdateTemplate_InvalidInputOrMissingDish_FailsWithMSG01()
    {
        SetupRepository(new List<WeeklyMenuTemplate> { new() { Id = 1, Name = "Mẫu 1", Items = new List<WeeklyMenuTemplateItem>() } });
        SetupRepository(new List<Dish>());

        var emptyName = await _menuService.UpdateTemplateAsync(1, 10, new UpsertWeeklyMenuTemplateRequestDto { Name = "" });
        var longDesc = await _menuService.UpdateTemplateAsync(1, 10, new UpsertWeeklyMenuTemplateRequestDto { Name = "Mẫu 1", Description = new string('X', 251) });
        var missingDish = await _menuService.UpdateTemplateAsync(1, 10, new UpsertWeeklyMenuTemplateRequestDto
        {
            Name = "Mẫu 1",
            Items = new() { new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 999 } }
        });

        emptyName.Code.Should().Be(SystemMessages.MSG01.Code);
        longDesc.Code.Should().Be(SystemMessages.MSG01.Code);
        missingDish.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    // ==================== 8. DeleteTemplateAsync (N/A) ====================

    [Fact]
    public async Task DeleteTemplate_ExistingTemplate_HardDeletesAndLogsAudit()
    {
        var templates = new List<WeeklyMenuTemplate> { new() { Id = 10, Name = "Mẫu Tuần Cũ" } };
        var repo = SetupRepository(templates);

        var res = await _menuService.DeleteTemplateAsync(10, 99);

        res.Success.Should().BeTrue();
        repo.Verify(r => r.DeleteAsync(It.Is<WeeklyMenuTemplate>(t => t.Id == 10), It.IsAny<CancellationToken>()), Times.Once);
        AuditServiceMock.Verify(a => a.LogActionAsync(99, AuditActions.LunchMenuTemplateDelete, nameof(WeeklyMenuTemplate), "10", It.IsAny<object?>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTemplate_NotFound_FailsWithMSG11()
    {
        SetupRepository(new List<WeeklyMenuTemplate>());

        var res = await _menuService.DeleteTemplateAsync(999, 99);

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    // ==================== 9. ApplyTemplateAsync (additional N/A/B) ====================

    [Fact]
    public async Task ApplyTemplate_OverwriteFalse_SkipsExistingAndNotServingDays()
    {
        var mon = new DateTime(2026, 11, 2);
        var tue = new DateTime(2026, 11, 3);
        var wed = new DateTime(2026, 11, 4);
        var dish = new Dish { Id = 1, Name = "Cơm", IsActive = true };
        var template = new WeeklyMenuTemplate
        {
            Id = 10, Name = "Mẫu", IsActive = true,
            Items = new List<WeeklyMenuTemplateItem>
            {
                new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, Dish = dish },
                new() { DayOfWeek = 2, MealType = MealType.StandardLunch, DishId = 1, Dish = dish },
                new() { DayOfWeek = 3, MealType = MealType.StandardLunch, DishId = 1, Dish = dish }
            }
        };
        SetupRepository(new List<WeeklyMenuTemplate> { template });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>
        {
            new() { Id = 1, Date = mon, IsServing = true, IsLocked = false, Dishes = new List<LunchServiceDayDish> { new() { Id = 1, DishId = 1, Dish = dish } } },
            new() { Id = 2, Date = tue, IsServing = false, IsLocked = false }
        });
        SetupRepository(new List<LunchServiceDayDish>());

        var res = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto
        {
            FromDate = mon,
            ToDate = wed,
            OverwriteExistingMenus = false
        });

        res.Success.Should().BeTrue();
        res.Data!.SkippedExistingDates.Should().ContainSingle(d => d == mon);
        res.Data.SkippedLockedDates.Should().ContainSingle(d => d == tue);
        res.Data.AppliedDates.Should().ContainSingle(d => d == wed);
    }

    [Fact]
    public async Task ApplyTemplate_InvalidDateRangeOrExceeds62Days_FailsWithMSG01()
    {
        var start = new DateTime(2026, 11, 2);

        var endBefore = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto { FromDate = start.AddDays(1), ToDate = start });
        var over62 = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto { FromDate = start, ToDate = start.AddDays(63) });

        endBefore.Code.Should().Be(SystemMessages.MSG01.Code);
        over62.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ApplyTemplate_InvalidCutoffTime_FailsWithMSG01()
    {
        var start = new DateTime(2026, 11, 2);

        var badFormat = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto { FromDate = start, ToDate = start, CutoffTime = "bad" });
        var outOfRange = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto { FromDate = start, ToDate = start, CutoffTime = "12:00" });

        badFormat.Code.Should().Be(SystemMessages.MSG01.Code);
        outOfRange.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task ApplyTemplate_NotFoundOrInactive_FailsWithMSG11()
    {
        var start = new DateTime(2026, 11, 2);
        SetupRepository(new List<WeeklyMenuTemplate> { new() { Id = 10, Name = "Inactive", IsActive = false } });

        var notFound = await _menuService.ApplyTemplateAsync(999, 99, new ApplyMenuTemplateRequestDto { FromDate = start, ToDate = start });
        var inactive = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto { FromDate = start, ToDate = start });

        notFound.Code.Should().Be(SystemMessages.MSG11.Code);
        inactive.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task ApplyTemplate_Exact62DaysAndMissingWeekdayInTemplate_SucceedsAndSkipsMissingDay()
    {
        var start = new DateTime(2026, 11, 2); // Monday
        var dish = new Dish { Id = 1, Name = "Cơm", IsActive = true };
        var template = new WeeklyMenuTemplate
        {
            Id = 10, Name = "Chỉ Thứ 2", IsActive = true,
            Items = new List<WeeklyMenuTemplateItem> { new() { DayOfWeek = 1, MealType = MealType.StandardLunch, DishId = 1, Dish = dish } }
        };
        SetupRepository(new List<WeeklyMenuTemplate> { template });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<LunchServiceDayDish>());

        var res = await _menuService.ApplyTemplateAsync(10, 99, new ApplyMenuTemplateRequestDto
        {
            FromDate = start,
            ToDate = start.AddDays(62),
            CutoffTime = "08:00"
        });

        res.Success.Should().BeTrue();
        res.Data!.AppliedDates.Should().NotBeEmpty();
        res.Data.SkippedNonSchoolDates.Should().Contain(start.AddDays(1)); // Thứ 3 không có trong mẫu
    }

    // ==================== 10. GetStudentMealProfileAsync (N/A) ====================

    [Fact]
    public async Task GetStudentMealProfile_LinkedParentWithExistingProfile_ReturnsProfile()
    {
        var updatedAt = new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Unspecified);
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<Guardian> { new() { Id = 1, UserId = 50, StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1 } } } });
        SetupRepository(new List<StudentMealProfile>
        {
            new() { Id = 1, StudentId = 1, DefaultMealType = MealType.SpecialDiet, AllergenTags = "seafood,egg", AllergyNotes = "Kiêng tôm", UpdatedAt = updatedAt }
        });

        var res = await _menuService.GetStudentMealProfileAsync(1, 50, new[] { UserRoles.Parent });

        res.Success.Should().BeTrue();
        res.Data!.DefaultMealType.Should().Be(MealType.SpecialDiet);
        res.Data.AllergenTags.Should().BeEquivalentTo(new[] { "seafood", "egg" });
        res.Data.UpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetStudentMealProfile_HomeroomTeacherWithoutProfile_FallsBackToSpecialNeedsNotes()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An", SpecialNeedsNotes = "Dị ứng lạc" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 5, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 5, IsActive = true } });
        SetupRepository(new List<StudentMealProfile>());

        var res = await _menuService.GetStudentMealProfileAsync(1, 30, new[] { UserRoles.Teacher });

        res.Success.Should().BeTrue();
        res.Data!.DefaultMealType.Should().Be(MealType.StandardLunch);
        res.Data.AllergyNotes.Should().Be("Dị ứng lạc");
        res.Data.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStudentMealProfile_StudentNotFound_FailsWithMSG11()
    {
        SetupRepository(new List<Student>());

        var res = await _menuService.GetStudentMealProfileAsync(999, 50, new[] { UserRoles.ServiceStaff });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task GetStudentMealProfile_UnlinkedParent_FailsWithMSG10()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<Guardian>());

        var res = await _menuService.GetStudentMealProfileAsync(1, 50, new[] { UserRoles.Parent });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task GetStudentMealProfile_NonHomeroomTeacher_FailsWithMSG10()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 5, ClassName = "1A", HomeroomTeacherId = 10, Status = "Active" } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 99, IsActive = true } });

        var res = await _menuService.GetStudentMealProfileAsync(1, 30, new[] { UserRoles.Teacher });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    // ==================== 11. UpsertStudentMealProfileAsync (additional N/A/B) ====================

    [Fact]
    public async Task UpsertStudentMealProfile_StudentNotFound_FailsWithMSG11()
    {
        SetupRepository(new List<Student>());

        var res = await _menuService.UpsertStudentMealProfileAsync(999, 99, new[] { UserRoles.ServiceStaff }, new UpsertStudentMealProfileRequestDto());

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UpsertStudentMealProfile_TeacherForbidden_FailsWithMSG10()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });

        var res = await _menuService.UpsertStudentMealProfileAsync(1, 30, new[] { UserRoles.Teacher }, new UpsertStudentMealProfileRequestDto());

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UpsertStudentMealProfile_AllergyNotesBoundary250And251Chars()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<StudentMealProfile>());

        var overRes = await _menuService.UpsertStudentMealProfileAsync(1, 99, new[] { UserRoles.ServiceStaff }, new UpsertStudentMealProfileRequestDto
        {
            DefaultMealType = MealType.StandardLunch,
            AllergyNotes = new string('A', 251)
        });
        var exactRes = await _menuService.UpsertStudentMealProfileAsync(1, 99, new[] { UserRoles.ServiceStaff }, new UpsertStudentMealProfileRequestDto
        {
            DefaultMealType = MealType.StandardLunch,
            AllergyNotes = new string('A', 250)
        });

        overRes.Success.Should().BeFalse();
        overRes.Code.Should().Be(SystemMessages.MSG01.Code);
        exactRes.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertStudentMealProfile_StaffSpecialDietWithoutTagsOrNotes_FailsWithMSG01()
    {
        SetupRepository(new List<Student> { new() { Id = 1, FullName = "Bé An" } });
        SetupRepository(new List<StudentMealProfile>());

        var res = await _menuService.UpsertStudentMealProfileAsync(1, 99, new[] { UserRoles.ServiceStaff }, new UpsertStudentMealProfileRequestDto
        {
            DefaultMealType = MealType.SpecialDiet,
            AllergenTags = new List<string>(),
            AllergyNotes = "  "
        });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
    }
}
