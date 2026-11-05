SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    -- 1. Create temporary table of school days between 2026-09-01 and 2026-11-30
    DECLARE @Dates TABLE (SchoolDate DATE, DayIndex INT IDENTITY(1,1));
    DECLARE @CurDate DATE = '2026-09-01';
    DECLARE @EndDate DATE = '2026-11-30';

    WHILE @CurDate <= @EndDate
    BEGIN
        -- Exclude Saturday (7), Sunday (1 in DATEFIRST 7), and Vietnam National Day 2026-09-02
        IF DATEPART(WEEKDAY, @CurDate) NOT IN (1, 7) AND @CurDate <> '2026-09-02'
        BEGIN
            INSERT INTO @Dates (SchoolDate) VALUES (@CurDate);
        END
        SET @CurDate = DATEADD(DAY, 1, @CurDate);
    END

    -- 2. Teacher user id to record
    DECLARE @TeacherUserId INT = (SELECT TOP 1 Id FROM Users WHERE Username = 'teacher1');
    IF @TeacherUserId IS NULL SET @TeacherUserId = 1;

    -- 3. Insert attendance for all active class assignments
    INSERT INTO AttendanceRecords (StudentId, ClassId, Date, Session, Status, Remarks, RecordedByUserId, CreatedAt)
    SELECT 
        ca.StudentId,
        ca.ClassId,
        d.SchoolDate,
        sess.SessionId,
        -- Status logic
        CASE 
            WHEN stu.StudentCode = 'HS005' AND d.SchoolDate IN ('2026-09-15', '2026-09-16', '2026-10-14', '2026-10-15') THEN 2 -- Excused
            WHEN stu.StudentCode = 'HS011' AND sess.SessionId = 1 AND (d.DayIndex % 5 = 0) THEN 4 -- Late
            WHEN stu.StudentCode = 'HS038' AND d.SchoolDate IN ('2026-09-22', '2026-10-20') THEN 2 -- Excused
            WHEN (ca.StudentId + d.DayIndex * 7 + sess.SessionId) % 37 = 0 THEN 4 -- Late (~2.7%)
            WHEN (ca.StudentId + d.DayIndex * 11 + sess.SessionId) % 41 = 0 THEN 2 -- Excused (~2.4%)
            WHEN (ca.StudentId + d.DayIndex * 13 + sess.SessionId) % 89 = 0 THEN 3 -- Unexcused (~1.1%)
            ELSE 1 -- Present (~93.8%)
        END AS Status,
        -- Remarks
        CASE 
            WHEN stu.StudentCode = 'HS005' AND d.SchoolDate IN ('2026-09-15', '2026-09-16', '2026-10-14', '2026-10-15') THEN N'Nghỉ ốm có đơn chỉ định của bác sĩ'
            WHEN stu.StudentCode = 'HS011' AND sess.SessionId = 1 AND (d.DayIndex % 5 = 0) THEN N'Đến muộn 15 phút do kẹt xe'
            WHEN stu.StudentCode = 'HS038' AND d.SchoolDate IN ('2026-09-22', '2026-10-20') THEN N'Gia đình có việc hiếu hỉ có đơn phép'
            WHEN (ca.StudentId + d.DayIndex * 7 + sess.SessionId) % 37 = 0 THEN N'Đến muộn 10-15 phút do thời tiết / kẹt xe'
            WHEN (ca.StudentId + d.DayIndex * 11 + sess.SessionId) % 41 = 0 THEN N'Nghỉ có đơn xin phép từ gia đình'
            WHEN (ca.StudentId + d.DayIndex * 13 + sess.SessionId) % 89 = 0 THEN N'Vắng không rõ lý do, GVCN đã liên hệ gia đình'
            ELSE NULL
        END AS Remarks,
        @TeacherUserId,
        CAST(d.SchoolDate AS DATETIME2)
    FROM ClassAssignments ca
    JOIN Students stu ON ca.StudentId = stu.Id
    CROSS JOIN @Dates d
    CROSS JOIN (SELECT 1 AS SessionId UNION ALL SELECT 2) sess
    WHERE ca.IsActive = 1
      AND NOT EXISTS (
          SELECT 1 
          FROM AttendanceRecords ar 
          WHERE ar.StudentId = ca.StudentId 
            AND ar.Date = d.SchoolDate 
            AND ar.Session = sess.SessionId
      );

    COMMIT TRANSACTION;
    PRINT 'SUCCESS: Full attendance records created for September, October, and November 2026.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT 'ERROR: ' + @ErrorMessage;
    THROW;
END CATCH;