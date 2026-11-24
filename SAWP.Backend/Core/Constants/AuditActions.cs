namespace Core.Constants;

// Every audit log action name. Format: UPPER_SNAKE_CASE, business object first, then the verb
// in present tense (e.g. CLASS_CREATE, LEAVE_REQUEST_APPROVE). Write audit entries only through
// IAuditService.LogActionAsync with one of these constants; never use a string literal.
public static class AuditActions
{
    // Users and system settings (System Administrator)
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdateRoles = "USER_UPDATE_ROLES";
    public const string UserActivate = "USER_ACTIVATE";
    public const string UserDeactivate = "USER_DEACTIVATE";
    public const string UserResetPassword = "USER_RESET_PASSWORD";
    public const string UserChangePassword = "USER_CHANGE_PASSWORD";
    public const string UserRecoverPassword = "USER_RECOVER_PASSWORD"; // new password set through an emailed link (UC-66)
    public const string UserActivateByLink = "USER_ACTIVATE_BY_LINK"; // first password set through the activation link (UC-66 A3)
    public const string UserActivationLinkSent = "USER_ACTIVATION_LINK_SENT";
    public const string SchoolCalendarDayConfigure = "SCHOOL_CALENDAR_DAY_CONFIGURE";

    // School years, classes and placement
    public const string SchoolYearCreate = "SCHOOL_YEAR_CREATE";
    public const string SchoolYearActivate = "SCHOOL_YEAR_ACTIVATE";
    public const string SchoolYearDelete = "SCHOOL_YEAR_DELETE";
    public const string SchoolYearPublishPlacements = "SCHOOL_YEAR_PUBLISH_PLACEMENTS";
    public const string ClassCreate = "CLASS_CREATE";
    public const string ClassInitializeBatch = "CLASS_INITIALIZE_BATCH";
    public const string ClassUpdate = "CLASS_UPDATE";
    public const string ClassAssignHomeroomTeacher = "CLASS_ASSIGN_HOMEROOM_TEACHER";
    public const string ClassUnassignHomeroomTeacher = "CLASS_UNASSIGN_HOMEROOM_TEACHER";
    public const string ClassAssignStudents = "CLASS_ASSIGN_STUDENTS";
    public const string ClassRemoveStudent = "CLASS_REMOVE_STUDENT";
    public const string ClassDelete = "CLASS_DELETE";
    public const string ClassPublishPlacement = "CLASS_PUBLISH_PLACEMENT";
    public const string PlacementPlanGenerateDraft = "PLACEMENT_PLAN_GENERATE_DRAFT";
    public const string PlacementPlanMoveStudent = "PLACEMENT_PLAN_MOVE_STUDENT";
    public const string PlacementPlanSwapStudents = "PLACEMENT_PLAN_SWAP_STUDENTS";
    public const string PlacementPlanSubmit = "PLACEMENT_PLAN_SUBMIT";
    public const string PlacementPlanApprove = "PLACEMENT_PLAN_APPROVE";
    public const string PlacementPlanReject = "PLACEMENT_PLAN_REJECT";

    // Students
    public const string StudentCreate = "STUDENT_CREATE";
    public const string StudentUpdate = "STUDENT_UPDATE";
    public const string StudentActivate = "STUDENT_ACTIVATE";
    public const string StudentResolveDuplicate = "STUDENT_RESOLVE_DUPLICATE";
    public const string StudentBulkImport = "STUDENT_BULK_IMPORT";
    public const string StudentGuardianLink = "STUDENT_GUARDIAN_LINK";
    public const string StudentGuardianUpdate = "STUDENT_GUARDIAN_UPDATE";
    public const string StudentGuardianUnlink = "STUDENT_GUARDIAN_UNLINK";
    public const string AccountRequestSaveDraft = "ACCOUNT_REQUEST_SAVE_DRAFT";
    public const string AccountRequestSubmit = "ACCOUNT_REQUEST_SUBMIT";
    public const string AccountRequestDecide = "ACCOUNT_REQUEST_DECIDE";
    public const string AccountRequestCloseRows = "ACCOUNT_REQUEST_CLOSE_ROWS";
    public const string AccountRequestResubmit = "ACCOUNT_REQUEST_RESUBMIT";
    public const string AccountRequestResendActivation = "ACCOUNT_REQUEST_RESEND_ACTIVATION";

    // Attendance
    public const string AttendanceSubmit = "ATTENDANCE_SUBMIT";
    public const string AttendanceUpdate = "ATTENDANCE_UPDATE";
    public const string AttendanceCorrect = "ATTENDANCE_CORRECT";
    public const string AttendanceAbsenceNoticeSend = "ATTENDANCE_ABSENCE_NOTICE_SEND";

    // Requests
    public const string LeaveRequestSubmit = "LEAVE_REQUEST_SUBMIT";
    public const string LeaveRequestForward = "LEAVE_REQUEST_FORWARD";
    public const string LeaveRequestApprove = "LEAVE_REQUEST_APPROVE";
    public const string LeaveRequestReject = "LEAVE_REQUEST_REJECT";
    public const string LeaveRequestCancel = "LEAVE_REQUEST_CANCEL";
    public const string LeaveRequestRequestMoreInfo = "LEAVE_REQUEST_REQUEST_MORE_INFO";
    public const string LeaveRequestResubmit = "LEAVE_REQUEST_RESUBMIT";
    public const string LeaveRequestWithdraw = "LEAVE_REQUEST_WITHDRAW";
    public const string ClassTransferRequestExecute = "CLASS_TRANSFER_REQUEST_EXECUTE";
    public const string AdministrativeRequestReject = "ADMINISTRATIVE_REQUEST_REJECT";

    // Lunch and boarding
    public const string LunchServiceDayConfigure = "LUNCH_SERVICE_DAY_CONFIGURE";
    public const string LunchServiceDayConfigureCutoff = "LUNCH_SERVICE_DAY_CONFIGURE_CUTOFF";
    public const string LunchServiceDayLock = "LUNCH_SERVICE_DAY_LOCK";
    public const string LunchServiceDayUnlock = "LUNCH_SERVICE_DAY_UNLOCK";
    public const string LunchMealRestore = "LUNCH_MEAL_RESTORE";
    public const string LunchRegistrationWaitlist = "LUNCH_REGISTRATION_WAITLIST";
    public const string LunchRegistrationUpdateMealType = "LUNCH_REGISTRATION_UPDATE_MEAL_TYPE";
    public const string LunchMonthlyQuotaConfigure = "LUNCH_MONTHLY_QUOTA_CONFIGURE";
    public const string LunchMonthlyQuotaClear = "LUNCH_MONTHLY_QUOTA_CLEAR";
    public const string BoardingIncidentCreate = "BOARDING_INCIDENT_CREATE";

    // E-contact book
    public const string PostCommentDelete = "POST_COMMENT_DELETE";
}
