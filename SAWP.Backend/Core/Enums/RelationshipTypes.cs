namespace Core.Enums;

public enum RelationshipTypes
{
    Father = 1,
    Mother = 2,
    Guardian = 3,
    Other = 4
}

public static class RelationshipExtensions
{
    public static string ToVietnamese(this RelationshipTypes rel) => rel switch
    {
        RelationshipTypes.Father => "Bố (Cha)",
        RelationshipTypes.Mother => "Mẹ",
        RelationshipTypes.Guardian => "Người giám hộ",
        RelationshipTypes.Other => "Người thân khác",
        _ => "Phụ huynh"
    };
}
