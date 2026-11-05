SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @StudentId INT;
    DECLARE @StudentCode NVARCHAR(50);
    DECLARE @FullName NVARCHAR(200);
    DECLARE @Area NVARCHAR(200);
    DECLARE @Index INT = 1;

    DECLARE @Surname NVARCHAR(50);
    DECLARE @FatherName NVARCHAR(100);
    DECLARE @MotherName NVARCHAR(100);
    DECLARE @PrimaryPhone NVARCHAR(20);
    DECLARE @SecondaryPhone NVARCHAR(20);
    DECLARE @Occupation1 NVARCHAR(100);
    DECLARE @Occupation2 NVARCHAR(100);
    DECLARE @Address NVARCHAR(250);
    DECLARE @Cccd1 NVARCHAR(20);
    DECLARE @Cccd2 NVARCHAR(20);

    DECLARE @PrimaryUserId INT;
    DECLARE @PrimaryGuardianId INT;
    DECLARE @SecondaryUserId INT;
    DECLARE @SecondaryGuardianId INT;

    DECLARE student_cursor CURSOR FOR
    SELECT Id, StudentCode, FullName, ResidentialArea
    FROM Students
    ORDER BY Id;

    OPEN student_cursor;
    FETCH NEXT FROM student_cursor INTO @StudentId, @StudentCode, @FullName, @Area;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- 1. Determine names
        SET @Surname = LEFT(@FullName, CHARINDEX(' ', @FullName + ' ') - 1);
        
        SET @FatherName = @Surname + ' ' + CASE (@Index % 10)
            WHEN 0 THEN N'Văn Hùng'
            WHEN 1 THEN N'Quốc Tuấn'
            WHEN 2 THEN N'Minh Trí'
            WHEN 3 THEN N'Đức Thắng'
            WHEN 4 THEN N'Hoàng Long'
            WHEN 5 THEN N'Thanh Hải'
            WHEN 6 THEN N'Tiến Đạt'
            WHEN 7 THEN N'Gia Huy'
            WHEN 8 THEN N'Thành Nam'
            ELSE N'Văn Minh'
        END;

        SET @MotherName = CASE (@Index % 10)
            WHEN 0 THEN N'Nguyễn Thị Mai'
            WHEN 1 THEN N'Trần Thị Lan'
            WHEN 2 THEN N'Lê Thị Hương'
            WHEN 3 THEN N'Phạm Mỹ Hạnh'
            WHEN 4 THEN N'Hoàng Thúy Nga'
            WHEN 5 THEN N'Vũ Ngọc Bích'
            WHEN 6 THEN N'Đỗ Thanh Thảo'
            WHEN 7 THEN N'Bùi Kim Ngân'
            WHEN 8 THEN N'Phan Cẩm Tú'
            ELSE N'Dương Thảo Trang'
        END;

        SET @PrimaryPhone = '09' + RIGHT('0' + CAST(10 + (@Index % 70) AS VARCHAR(2)), 2) + RIGHT('000000' + CAST(123450 + @Index * 77 AS VARCHAR(10)), 6);
        SET @SecondaryPhone = '098' + RIGHT('0000000' + CAST(234500 + @Index * 89 AS VARCHAR(10)), 7);

        SET @Occupation1 = CASE (@Index % 8)
            WHEN 0 THEN N'Kỹ sư phần mềm'
            WHEN 1 THEN N'Bác sĩ đa khoa'
            WHEN 2 THEN N'Giáo viên'
            WHEN 3 THEN N'Kinh doanh tự do'
            WHEN 4 THEN N'Kế toán trưởng'
            WHEN 5 THEN N'Kiến trúc sư'
            WHEN 6 THEN N'Chuyên viên ngân hàng'
            ELSE N'Công chức nhà nước'
        END;

        SET @Occupation2 = CASE ((@Index + 3) % 8)
            WHEN 0 THEN N'Giáo viên'
            WHEN 1 THEN N'Dược sĩ'
            WHEN 2 THEN N'Kế toán viên'
            WHEN 3 THEN N'Nhân viên văn phòng'
            WHEN 4 THEN N'Bác sĩ nha khoa'
            WHEN 5 THEN N'Quản lý nhân sự'
            WHEN 6 THEN N'Chuyên viên tư vấn'
            ELSE N'Kinh doanh'
        END;

        SET @Address = CAST(12 + @Index AS NVARCHAR(10)) + N' Đường số ' + CAST(1 + (@Index % 12) AS NVARCHAR(10)) + N', ' + ISNULL(@Area, N'Phường Bến Nghé') + N', TP. Hồ Chí Minh';
        SET @Cccd1 = '001090' + RIGHT('000000' + CAST(100000 + @Index * 2 AS VARCHAR(10)), 6);
        SET @Cccd2 = '001090' + RIGHT('000000' + CAST(100001 + @Index * 2 AS VARCHAR(10)), 6);

        -- Check if student already has a primary guardian
        IF NOT EXISTS (SELECT 1 FROM StudentGuardians WHERE StudentId = @StudentId AND IsPrimaryContact = 1)
        BEGIN
            -- Create Primary Guardian (Father)
            DECLARE @Username1 NVARCHAR(100) = 'ph_' + LOWER(@StudentCode);
            DECLARE @Email1 NVARCHAR(100) = 'parent_' + LOWER(@StudentCode) + '@sawp.edu.vn';

            -- Ensure username is unique
            IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = @Username1)
            BEGIN
                INSERT INTO Users (Username, Email, PasswordHash, FullName, PhoneNumber, IsActive, CreatedAt)
                VALUES (@Username1, @Email1, '.PDGaVv8SyAox2xZlJbNqjpju3IIte', @FatherName, @PrimaryPhone, 1, GETUTCDATE());
                SET @PrimaryUserId = SCOPE_IDENTITY();

                -- Assign Role 6 (Parent)
                INSERT INTO UserRoles (UserId, RoleId, AssignedAt)
                VALUES (@PrimaryUserId, 6, GETUTCDATE());

                -- Insert Guardian profile
                INSERT INTO Guardians (UserId, IdentityCardNumber, Occupation, Address, CreatedAt)
                VALUES (@PrimaryUserId, @Cccd1, @Occupation1, @Address, GETUTCDATE());
                SET @PrimaryGuardianId = SCOPE_IDENTITY();

                -- Link Student to Guardian (Primary)
                INSERT INTO StudentGuardians (StudentId, GuardianId, Relationship, IsPrimaryContact)
                VALUES (@StudentId, @PrimaryGuardianId, 1, 1); -- 1 = Father
            END
        END

        -- Check if student already has a secondary/emergency contact
        IF NOT EXISTS (SELECT 1 FROM StudentGuardians WHERE StudentId = @StudentId AND IsPrimaryContact = 0)
        BEGIN
            -- Create Secondary Guardian (Mother)
            DECLARE @Username2 NVARCHAR(100) = 'ph2_' + LOWER(@StudentCode);
            DECLARE @Email2 NVARCHAR(100) = 'emergency_' + LOWER(@StudentCode) + '@sawp.edu.vn';

            IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = @Username2)
            BEGIN
                INSERT INTO Users (Username, Email, PasswordHash, FullName, PhoneNumber, IsActive, CreatedAt)
                VALUES (@Username2, @Email2, '.PDGaVv8SyAox2xZlJbNqjpju3IIte', @MotherName, @SecondaryPhone, 1, GETUTCDATE());
                SET @SecondaryUserId = SCOPE_IDENTITY();

                -- Assign Role 6 (Parent)
                INSERT INTO UserRoles (UserId, RoleId, AssignedAt)
                VALUES (@SecondaryUserId, 6, GETUTCDATE());

                -- Insert Guardian profile
                INSERT INTO Guardians (UserId, IdentityCardNumber, Occupation, Address, CreatedAt)
                VALUES (@SecondaryUserId, @Cccd2, @Occupation2, @Address, GETUTCDATE());
                SET @SecondaryGuardianId = SCOPE_IDENTITY();

                -- Link Student to Guardian (Secondary/Emergency)
                INSERT INTO StudentGuardians (StudentId, GuardianId, Relationship, IsPrimaryContact)
                VALUES (@StudentId, @SecondaryGuardianId, 2, 0); -- 2 = Mother
            END
        END

        SET @Index = @Index + 1;
        FETCH NEXT FROM student_cursor INTO @StudentId, @StudentCode, @FullName, @Area;
    END

    CLOSE student_cursor;
    DEALLOCATE student_cursor;

    COMMIT TRANSACTION;
    PRINT 'SUCCESS: Seeded guardians for all students.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT 'ERROR: ' + @ErrorMessage;
    THROW;
END CATCH;