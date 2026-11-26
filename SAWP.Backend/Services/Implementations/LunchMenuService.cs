using Core.Common;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class LunchMenuService : ILunchMenuService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchoolClock _clock;
    private readonly SchoolOptions _options;
    private readonly IAuditService _auditService;

    private static readonly TimeSpan MinCutoffTime = new(6, 0, 0);
    private static readonly TimeSpan MaxCutoffTime = new(11, 0, 0);

    public LunchMenuService(
        IUnitOfWork unitOfWork,
        ISchoolClock clock,
        IOptions<SchoolOptions> options,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
        _auditService = auditService;
    }

    // ==================== HELPER CHUNG ====================

    public static List<MealType> ParseMealTypes(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<MealType> { MealType.StandardLunch };
        var parsed = csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<MealType>(s, true, out var mt) ? (MealType?)mt : null)
            .Where(mt => mt.HasValue)
            .Select(mt => mt!.Value)
            .Distinct()
            .ToList();
        return parsed.Count > 0 ? parsed : new List<MealType> { MealType.StandardLunch };
    }

    public static string SerializeMealTypes(IEnumerable<MealType>? mealTypes)
    {
        var list = (mealTypes ?? Enumerable.Empty<MealType>()).Distinct().OrderBy(x => (int)x).ToList();
        if (list.Count == 0) list.Add(MealType.StandardLunch);
        return string.Join(",", list.Select(x => x.ToString()));
    }

    public static DishDto MapDishDto(Dish d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Category = d.Category,
        SuitableMealTypes = ParseMealTypes(d.SuitableMealTypes),
        AllergenTags = AllergenCatalog.ParseTags(d.AllergenTags),
        IsActive = d.IsActive,
        UpdatedAt = UtcDateTimeHelper.Normalize(d.UpdatedAt)
    };

    public static DishSummaryDto MapDishSummaryDto(Dish d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Category = d.Category,
        AllergenTags = AllergenCatalog.ParseTags(d.AllergenTags)
    };

    public static DailyMenusByMealTypeDto? BuildDailyMenusDto(IEnumerable<LunchServiceDayDish>? dayDishes)
    {
        if (dayDishes == null) return null;
        var items = dayDishes.Where(x => x.Dish != null).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        if (items.Count == 0) return null;

        return new DailyMenusByMealTypeDto
        {
            Standard = items.Where(x => x.MealType == MealType.StandardLunch).Select(x => MapDishSummaryDto(x.Dish)).ToList(),
            Vegetarian = items.Where(x => x.MealType == MealType.VegetarianLunch).Select(x => MapDishSummaryDto(x.Dish)).ToList(),
            Special = items.Where(x => x.MealType == MealType.SpecialDiet).Select(x => MapDishSummaryDto(x.Dish)).ToList()
        };
    }

    public static string? BuildSummaryRemarks(DailyMenusByMealTypeDto? menus, int maxLength = 250)
    {
        if (menus == null) return null;
        var source = menus.Standard.Count > 0
            ? menus.Standard
            : (menus.Vegetarian.Count > 0 ? menus.Vegetarian : menus.Special);

        if (source.Count == 0) return null;
        var joined = string.Join(", ", source.Select(d => d.Name));
        return joined.Length <= maxLength ? joined : joined[..maxLength];
    }

    // ==================== 1. THƯ VIỆN MÓN ĂN ====================

    public async Task<ApiResponse<List<DishDto>>> GetDishesAsync(
        DishCategory? category = null,
        MealType? mealType = null,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var dishRepo = _unitOfWork.Repository<Dish>();
        if (dishRepo == null) return ApiResponse<List<DishDto>>.Ok(new List<DishDto>());

        var query = dishRepo.Query();
        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }
        if (category.HasValue)
        {
            query = query.Where(d => d.Category == category.Value);
        }

        var list = await query
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);

        if (mealType.HasValue)
        {
            list = list.Where(d => ParseMealTypes(d.SuitableMealTypes).Contains(mealType.Value)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            list = list.Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return ApiResponse<List<DishDto>>.Ok(list.Select(MapDishDto).ToList());
    }

    public async Task<ApiResponse<DishDto>> CreateDishAsync(int userId, UpsertDishRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return ApiResponse<DishDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên món ăn không được để trống và tối đa 120 ký tự." });
        }

        var dishRepo = _unitOfWork.Repository<Dish>();
        var exists = await dishRepo.Query().AnyAsync(d => d.Name.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
        {
            return ApiResponse<DishDto>.Fail(SystemMessages.MSG09, new List<string> { $"Món ăn \"{name}\" đã tồn tại trong thư viện." });
        }

        var now = DateTime.UtcNow;
        var entity = new Dish
        {
            Name = name,
            Category = request.Category,
            SuitableMealTypes = SerializeMealTypes(request.SuitableMealTypes),
            AllergenTags = AllergenCatalog.NormalizeTagsCsv(request.AllergenTags),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        await dishRepo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchDishCreate,
            nameof(Dish),
            entity.Id.ToString(),
            null,
            new { entity.Name, entity.Category, entity.SuitableMealTypes, entity.AllergenTags },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<DishDto>.Ok(MapDishDto(entity), "Đã thêm món ăn mới vào thư viện.");
    }

    public async Task<ApiResponse<DishDto>> UpdateDishAsync(int dishId, int userId, UpsertDishRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return ApiResponse<DishDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên món ăn không được để trống và tối đa 120 ký tự." });
        }

        var dishRepo = _unitOfWork.Repository<Dish>();
        var entity = await dishRepo.Query().FirstOrDefaultAsync(d => d.Id == dishId, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<DishDto>.Fail(SystemMessages.MSG11, new List<string> { "Không tìm thấy món ăn." });
        }

        var duplicate = await dishRepo.Query()
            .AnyAsync(d => d.Id != dishId && d.Name.ToLower() == name.ToLower(), cancellationToken);
        if (duplicate)
        {
            return ApiResponse<DishDto>.Fail(SystemMessages.MSG09, new List<string> { $"Món ăn \"{name}\" đã tồn tại." });
        }

        var oldVal = new { entity.Name, entity.Category, entity.SuitableMealTypes, entity.AllergenTags, entity.IsActive };

        entity.Name = name;
        entity.Category = request.Category;
        entity.SuitableMealTypes = SerializeMealTypes(request.SuitableMealTypes);
        entity.AllergenTags = AllergenCatalog.NormalizeTagsCsv(request.AllergenTags);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchDishUpdate,
            nameof(Dish),
            entity.Id.ToString(),
            oldVal,
            new { entity.Name, entity.Category, entity.SuitableMealTypes, entity.AllergenTags, entity.IsActive },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<DishDto>.Ok(MapDishDto(entity), "Đã cập nhật thông tin món ăn.");
    }

    public async Task<ApiResponse<bool>> DeleteDishAsync(int dishId, int userId, CancellationToken cancellationToken = default)
    {
        var dishRepo = _unitOfWork.Repository<Dish>();
        var entity = await dishRepo.Query().FirstOrDefaultAsync(d => d.Id == dishId, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG11, new List<string> { "Không tìm thấy món ăn." });
        }

        var tplItemRepo = _unitOfWork.Repository<WeeklyMenuTemplateItem>();
        var dayDishRepo = _unitOfWork.Repository<LunchServiceDayDish>();

        var inTemplate = tplItemRepo != null && await tplItemRepo.Query().AnyAsync(i => i.DishId == dishId, cancellationToken);
        var inServiceDay = dayDishRepo != null && await dayDishRepo.Query().AnyAsync(s => s.DishId == dishId, cancellationToken);

        if (inTemplate || inServiceDay)
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;

            await _auditService.LogActionAsync(
                userId > 0 ? userId : null,
                AuditActions.LunchDishDelete,
                nameof(Dish),
                entity.Id.ToString(),
                new { IsActive = true },
                new { IsActive = false, Mode = "SoftDeactivate" },
                null,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Ok(true, $"Món \"{entity.Name}\" đang được sử dụng trong thực đơn nên đã được chuyển sang trạng thái Ngưng dùng.");
        }

        await dishRepo.DeleteAsync(entity, cancellationToken);
        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchDishDelete,
            nameof(Dish),
            dishId.ToString(),
            new { entity.Name },
            new { Mode = "HardDelete" },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Đã xóa món \"{entity.Name}\" khỏi thư viện.");
    }

    // ==================== 2. MẪU THỰC ĐƠN TUẦN ====================

    public async Task<ApiResponse<List<WeeklyMenuTemplateDto>>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
        if (tplRepo == null) return ApiResponse<List<WeeklyMenuTemplateDto>>.Ok(new List<WeeklyMenuTemplateDto>());

        var query = tplRepo.Query()
            .Include(t => t.Items)
                .ThenInclude(i => i.Dish)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return ApiResponse<List<WeeklyMenuTemplateDto>>.Ok(templates.Select(MapTemplateDto).ToList());
    }

    private static WeeklyMenuTemplateDto MapTemplateDto(WeeklyMenuTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        IsActive = t.IsActive,
        UpdatedAt = UtcDateTimeHelper.Normalize(t.UpdatedAt),
        Items = t.Items
            .Where(i => i.Dish != null)
            .OrderBy(i => i.DayOfWeek)
            .ThenBy(i => (int)i.MealType)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => new WeeklyMenuTemplateItemDto
            {
                DayOfWeek = i.DayOfWeek,
                MealType = i.MealType,
                DishId = i.DishId,
                DishName = i.Dish.Name,
                Category = i.Dish.Category,
                AllergenTags = AllergenCatalog.ParseTags(i.Dish.AllergenTags),
                SortOrder = i.SortOrder
            })
            .ToList()
    };

    private async Task<(bool Ok, string? ErrorMessage, List<WeeklyMenuTemplateItem> Items)> ValidateAndBuildTemplateItemsAsync(
        List<WeeklyMenuTemplateItemInputDto>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || inputs.Count == 0)
        {
            return (false, "Thực đơn tuần phải có ít nhất một món ăn.", new List<WeeklyMenuTemplateItem>());
        }

        var invalidDay = inputs.Any(i => i.DayOfWeek < 1 || i.DayOfWeek > 6);
        if (invalidDay)
        {
            return (false, "Thứ trong thực đơn tuần chỉ nhận giá trị từ 1 (Thứ 2) đến 6 (Thứ 7).", new List<WeeklyMenuTemplateItem>());
        }

        var dishIds = inputs.Select(i => i.DishId).Distinct().ToList();
        var dishRepo = _unitOfWork.Repository<Dish>();
        var validDishIds = (await dishRepo.Query()
            .Where(d => dishIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        if (validDishIds.Count != dishIds.Count)
        {
            return (false, "Một hoặc nhiều món ăn trong thực đơn tuần không tồn tại.", new List<WeeklyMenuTemplateItem>());
        }

        var deduped = inputs
            .GroupBy(i => new { i.DayOfWeek, i.MealType, i.DishId })
            .Select((g, idx) => new WeeklyMenuTemplateItem
            {
                DayOfWeek = g.Key.DayOfWeek,
                MealType = g.Key.MealType,
                DishId = g.Key.DishId,
                SortOrder = g.First().SortOrder > 0 ? g.First().SortOrder : idx + 1
            })
            .ToList();

        return (true, null, deduped);
    }

    public async Task<ApiResponse<WeeklyMenuTemplateDto>> CreateTemplateAsync(int userId, UpsertWeeklyMenuTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên thực đơn tuần không được để trống và tối đa 120 ký tự." });
        }

        var desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (desc != null && desc.Length > 250)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Mô tả thực đơn tuần tối đa 250 ký tự." });
        }

        var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
        if (await tplRepo.Query().AnyAsync(t => t.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG09, new List<string> { $"Thực đơn tuần \"{name}\" đã tồn tại." });
        }

        var (ok, err, builtItems) = await ValidateAndBuildTemplateItemsAsync(request.Items, cancellationToken);
        if (!ok)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { err! });
        }

        var now = DateTime.UtcNow;
        var entity = new WeeklyMenuTemplate
        {
            Name = name,
            Description = desc,
            IsActive = request.IsActive,
            CreatedByUserId = userId > 0 ? userId : null,
            CreatedAt = now,
            UpdatedAt = now,
            Items = builtItems
        };

        await tplRepo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchMenuTemplateCreate,
            nameof(WeeklyMenuTemplate),
            entity.Id.ToString(),
            null,
            new { entity.Name, ItemCount = builtItems.Count },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var loaded = await tplRepo.Query()
            .Include(t => t.Items).ThenInclude(i => i.Dish)
            .FirstAsync(t => t.Id == entity.Id, cancellationToken);
        return ApiResponse<WeeklyMenuTemplateDto>.Ok(MapTemplateDto(loaded), "Đã tạo thực đơn tuần thành công.");
    }

    public async Task<ApiResponse<WeeklyMenuTemplateDto>> UpdateTemplateAsync(int templateId, int userId, UpsertWeeklyMenuTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên thực đơn tuần không được để trống và tối đa 120 ký tự." });
        }

        var desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (desc != null && desc.Length > 250)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { "Mô tả thực đơn tuần tối đa 250 ký tự." });
        }

        var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
        var entity = await tplRepo.Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG11, new List<string> { "Không tìm thấy thực đơn tuần." });
        }

        if (await tplRepo.Query().AnyAsync(t => t.Id != templateId && t.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG09, new List<string> { $"Thực đơn tuần \"{name}\" đã tồn tại." });
        }

        var (ok, err, builtItems) = await ValidateAndBuildTemplateItemsAsync(request.Items, cancellationToken);
        if (!ok)
        {
            return ApiResponse<WeeklyMenuTemplateDto>.Fail(SystemMessages.MSG01, new List<string> { err! });
        }

        var oldSummary = new { entity.Name, entity.Description, entity.IsActive, ItemCount = entity.Items.Count };

        var itemRepo = _unitOfWork.Repository<WeeklyMenuTemplateItem>();
        foreach (var oldItem in entity.Items.ToList())
        {
            if (itemRepo != null)
            {
                await itemRepo.DeleteAsync(oldItem, cancellationToken);
            }
            entity.Items.Remove(oldItem);
        }

        entity.Name = name;
        entity.Description = desc;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        foreach (var newItem in builtItems)
        {
            newItem.TemplateId = entity.Id;
            entity.Items.Add(newItem);
        }

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchMenuTemplateUpdate,
            nameof(WeeklyMenuTemplate),
            entity.Id.ToString(),
            oldSummary,
            new { entity.Name, entity.Description, entity.IsActive, ItemCount = builtItems.Count },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var loaded = await tplRepo.Query()
            .Include(t => t.Items).ThenInclude(i => i.Dish)
            .FirstAsync(t => t.Id == entity.Id, cancellationToken);
        return ApiResponse<WeeklyMenuTemplateDto>.Ok(MapTemplateDto(loaded), "Đã cập nhật thực đơn tuần.");
    }

    public async Task<ApiResponse<bool>> DeleteTemplateAsync(int templateId, int userId, CancellationToken cancellationToken = default)
    {
        var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
        var entity = await tplRepo.Query().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG11, new List<string> { "Không tìm thấy thực đơn tuần." });
        }

        await tplRepo.DeleteAsync(entity, cancellationToken);
        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchMenuTemplateDelete,
            nameof(WeeklyMenuTemplate),
            templateId.ToString(),
            new { entity.Name },
            null,
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Đã xóa thực đơn tuần \"{entity.Name}\".");
    }

    public async Task<ApiResponse<ApplyMenuTemplateResultDto>> ApplyTemplateAsync(
        int templateId,
        int userId,
        ApplyMenuTemplateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var start = request.FromDate.Date;
        var end = request.ToDate.Date;
        if (end < start || (end - start).TotalDays > 62)
        {
            return ApiResponse<ApplyMenuTemplateResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Khoảng ngày áp dụng thực đơn tuần không hợp lệ (tối đa 62 ngày)." });
        }

        TimeSpan? customCutoff = null;
        if (!string.IsNullOrWhiteSpace(request.CutoffTime))
        {
            if (!TimeSpan.TryParse(request.CutoffTime, out var parsedCutoff) || parsedCutoff < MinCutoffTime || parsedCutoff > MaxCutoffTime)
            {
                return ApiResponse<ApplyMenuTemplateResultDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Giờ chốt suất ăn (CutoffTime) phải trong khoảng 06:00 đến 11:00." });
            }
            customCutoff = parsedCutoff;
        }

        var tplRepo = _unitOfWork.Repository<WeeklyMenuTemplate>();
        var template = await tplRepo.Query()
            .Include(t => t.Items)
                .ThenInclude(i => i.Dish)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
        if (template == null || !template.IsActive)
        {
            return ApiResponse<ApplyMenuTemplateResultDto>.Fail(SystemMessages.MSG11, new List<string> { "Không tìm thấy thực đơn tuần đang hoạt động." });
        }

        var calRepo = _unitOfWork.Repository<SchoolCalendarDay>();
        var calendarMap = calRepo == null
            ? new Dictionary<DateTime, bool>()
            : (await calRepo.Query()
                .Where(d => d.Date >= start && d.Date <= end)
                .ToListAsync(cancellationToken))
                .GroupBy(d => d.Date.Date)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().IsSchoolDay);

        var serviceDayRepo = _unitOfWork.Repository<LunchServiceDay>();
        var existingDays = await serviceDayRepo.Query()
            .Include(d => d.Dishes)
            .Where(d => d.Date >= start && d.Date <= end)
            .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

        var dayDishRepo = _unitOfWork.Repository<LunchServiceDayDish>();
        var result = new ApplyMenuTemplateResultDto();

        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            if (!SchoolCalendarHelper.IsSchoolDay(cur, calendarMap))
            {
                result.SkippedNonSchoolDates.Add(cur);
                continue;
            }

            existingDays.TryGetValue(cur, out var sDay);
            if (sDay != null && (sDay.IsLocked || !sDay.IsServing))
            {
                result.SkippedLockedDates.Add(cur);
                continue;
            }

            if (sDay != null && sDay.Dishes.Count > 0 && !request.OverwriteExistingMenus)
            {
                result.SkippedExistingDates.Add(cur);
                continue;
            }

            var dow = cur.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)cur.DayOfWeek;
            var templateItems = template.Items
                .Where(i => i.DayOfWeek == dow && i.Dish != null && i.Dish.IsActive)
                .ToList();
            if (templateItems.Count == 0 && dow == 6)
            {
                // Nếu thứ 7 học bù chưa định nghĩa riêng trong mẫu thì mượn thực đơn Thứ 2 của mẫu
                templateItems = template.Items
                    .Where(i => i.DayOfWeek == 1 && i.Dish != null && i.Dish.IsActive)
                    .ToList();
            }

            if (templateItems.Count == 0)
            {
                result.SkippedNonSchoolDates.Add(cur);
                continue;
            }

            if (sDay == null)
            {
                sDay = new LunchServiceDay
                {
                    Date = cur,
                    IsServing = true,
                    CutoffTime = customCutoff ?? _options.LunchCutoff,
                    IsLocked = false
                };
                await serviceDayRepo.AddAsync(sDay, cancellationToken);
                existingDays[cur] = sDay;
            }
            else if (customCutoff.HasValue)
            {
                sDay.CutoffTime = customCutoff.Value;
            }

            // Xóa món cũ trong ngày trước khi áp món mới từ mẫu
            foreach (var oldDish in sDay.Dishes.ToList())
            {
                if (dayDishRepo != null)
                {
                    await dayDishRepo.DeleteAsync(oldDish, cancellationToken);
                }
                sDay.Dishes.Remove(oldDish);
            }

            var appliedDayDishes = new List<LunchServiceDayDish>();
            foreach (var item in templateItems)
            {
                var newDayDish = new LunchServiceDayDish
                {
                    LunchServiceDayId = sDay.Id,
                    LunchServiceDay = sDay,
                    MealType = item.MealType,
                    DishId = item.DishId,
                    Dish = item.Dish,
                    SortOrder = item.SortOrder
                };
                sDay.Dishes.Add(newDayDish);
                appliedDayDishes.Add(newDayDish);
            }

            var menusDto = BuildDailyMenusDto(appliedDayDishes);
            sDay.Remarks = BuildSummaryRemarks(menusDto);
            result.AppliedDates.Add(cur);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.LunchMenuTemplateApply,
            nameof(WeeklyMenuTemplate),
            template.Id.ToString(),
            null,
            new
            {
                TemplateName = template.Name,
                FromDate = start.ToString("yyyy-MM-dd"),
                ToDate = end.ToString("yyyy-MM-dd"),
                AppliedCount = result.AppliedDates.Count,
                SkippedLockedCount = result.SkippedLockedDates.Count
            },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<ApplyMenuTemplateResultDto>.Ok(
            result,
            $"Đã áp dụng \"{template.Name}\" cho {result.AppliedDates.Count} ngày học.");
    }

    // ==================== 3. HỒ SƠ CHẾ ĐỘ ĂN & DỊ ỨNG HỌC SINH ====================

    private async Task<bool> CanViewStudentProfileAsync(int studentId, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct)
    {
        if (roles.Contains(UserRoles.Admin) ||
            roles.Contains(UserRoles.ServiceStaff) ||
            roles.Contains(UserRoles.OfficeStaff) ||
            roles.Contains(UserRoles.SchoolBoard))
        {
            return true;
        }

        if (roles.Contains(UserRoles.Teacher))
        {
            var staffRepo = _unitOfWork.Repository<StaffProfile>();
            var classRepo = _unitOfWork.Repository<Class>();
            var caRepo = _unitOfWork.Repository<ClassAssignment>();
            if (staffRepo != null && classRepo != null && caRepo != null)
            {
                var staff = await staffRepo.Query().FirstOrDefaultAsync(sp => sp.UserId == userId, ct);
                if (staff != null)
                {
                    var homeroomIds = await classRepo.Query()
                        .Where(c => c.HomeroomTeacherId == staff.Id && c.Status == "Active")
                        .Select(c => c.Id)
                        .ToListAsync(ct);

                    if (homeroomIds.Count > 0)
                    {
                        var inClass = await caRepo.Query()
                            .AnyAsync(ca => ca.StudentId == studentId && ca.IsActive && homeroomIds.Contains(ca.ClassId), ct);
                        if (inClass) return true;
                    }
                }
            }
        }

        if (roles.Contains(UserRoles.Parent))
        {
            var guardianRepo = _unitOfWork.Repository<Guardian>();
            if (guardianRepo != null)
            {
                var guardian = await guardianRepo.Query()
                    .Include(g => g.StudentGuardians)
                    .FirstOrDefaultAsync(g => g.UserId == userId, ct);
                if (guardian != null && guardian.StudentGuardians.Any(sg => sg.StudentId == studentId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> CanEditStudentProfileAsync(int studentId, int userId, IReadOnlyList<UserRoles> roles, CancellationToken ct)
    {
        if (roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff))
        {
            return true;
        }

        if (roles.Contains(UserRoles.Parent))
        {
            var guardianRepo = _unitOfWork.Repository<Guardian>();
            if (guardianRepo != null)
            {
                var guardian = await guardianRepo.Query()
                    .Include(g => g.StudentGuardians)
                    .FirstOrDefaultAsync(g => g.UserId == userId, ct);
                return guardian != null && guardian.StudentGuardians.Any(sg => sg.StudentId == studentId);
            }
        }

        return false;
    }

    public async Task<ApiResponse<StudentMealProfileDto>> GetStudentMealProfileAsync(
        int studentId,
        int userId,
        IReadOnlyList<UserRoles> roles,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Repository<Student>().Query()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentMealProfileDto>.Fail(SystemMessages.MSG11);
        }

        if (!await CanViewStudentProfileAsync(studentId, userId, roles, cancellationToken))
        {
            return ApiResponse<StudentMealProfileDto>.Fail(SystemMessages.MSG10);
        }

        var profileRepo = _unitOfWork.Repository<StudentMealProfile>();
        var profile = profileRepo == null
            ? null
            : await profileRepo.Query().FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

        return ApiResponse<StudentMealProfileDto>.Ok(BuildProfileDto(student, profile));
    }

    public static StudentMealProfileDto BuildProfileDto(Student student, StudentMealProfile? profile)
    {
        if (profile == null)
        {
            return new StudentMealProfileDto
            {
                StudentId = student.Id,
                DefaultMealType = MealType.StandardLunch,
                AllergenTags = new List<string>(),
                AllergyNotes = string.IsNullOrWhiteSpace(student.SpecialNeedsNotes) ? null : student.SpecialNeedsNotes,
                UpdatedAt = null
            };
        }

        return new StudentMealProfileDto
        {
            StudentId = student.Id,
            DefaultMealType = profile.DefaultMealType,
            AllergenTags = AllergenCatalog.ParseTags(profile.AllergenTags),
            AllergyNotes = profile.AllergyNotes,
            UpdatedAt = UtcDateTimeHelper.Normalize(profile.UpdatedAt)
        };
    }

    public async Task<ApiResponse<StudentMealProfileDto>> UpsertStudentMealProfileAsync(
        int studentId,
        int userId,
        IReadOnlyList<UserRoles> roles,
        UpsertStudentMealProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Repository<Student>().Query()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentMealProfileDto>.Fail(SystemMessages.MSG11);
        }

        if (!await CanEditStudentProfileAsync(studentId, userId, roles, cancellationToken))
        {
            return ApiResponse<StudentMealProfileDto>.Fail(SystemMessages.MSG10);
        }

        var notes = string.IsNullOrWhiteSpace(request.AllergyNotes) ? null : request.AllergyNotes.Trim();
        if (notes != null && notes.Length > 250)
        {
            return ApiResponse<StudentMealProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Ghi chú dị ứng tối đa 250 ký tự." });
        }

        var isStaffOrAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.ServiceStaff);
        var profileRepo = _unitOfWork.Repository<StudentMealProfile>();
        var existing = await profileRepo.Query().FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

        // Phụ huynh chỉ được khai báo dị ứng thực phẩm & ghi chú sức khỏe; chỉ Tổ Bán trú / Admin mới có quyền phân bổ DefaultMealType
        var effectiveDefaultMealType = isStaffOrAdmin
            ? request.DefaultMealType
            : (existing?.DefaultMealType ?? MealType.StandardLunch);

        var normalizedTags = AllergenCatalog.NormalizeTagsCsv(request.AllergenTags);
        if (effectiveDefaultMealType == MealType.SpecialDiet && string.IsNullOrEmpty(normalizedTags) && string.IsNullOrEmpty(notes))
        {
            return ApiResponse<StudentMealProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Khi phân bổ chế độ ăn mặc định là Đặc biệt, vui lòng chọn ít nhất 1 nhóm dị ứng hoặc nhập ghi chú chi tiết." });
        }

        var oldVal = existing == null
            ? null
            : new { existing.DefaultMealType, existing.AllergenTags, existing.AllergyNotes };

        if (existing == null)
        {
            existing = new StudentMealProfile
            {
                StudentId = studentId,
                DefaultMealType = effectiveDefaultMealType,
                AllergenTags = normalizedTags,
                AllergyNotes = notes,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = userId > 0 ? userId : null
            };
            await profileRepo.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.DefaultMealType = effectiveDefaultMealType;
            existing.AllergenTags = normalizedTags;
            existing.AllergyNotes = notes;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = userId > 0 ? userId : null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            userId > 0 ? userId : null,
            AuditActions.StudentMealProfileUpdate,
            nameof(StudentMealProfile),
            existing.Id.ToString(),
            oldVal,
            new { existing.DefaultMealType, existing.AllergenTags, existing.AllergyNotes },
            null,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var msg = isStaffOrAdmin
            ? "Đã lưu phân bổ chế độ khẩu phần và thông tin dị ứng của học sinh."
            : "Đã lưu khai báo dị ứng thực phẩm của học sinh.";
        return ApiResponse<StudentMealProfileDto>.Ok(BuildProfileDto(student, existing), msg);
    }
}
