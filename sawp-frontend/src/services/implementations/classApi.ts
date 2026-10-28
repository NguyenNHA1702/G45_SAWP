import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  AssignStudentsDto,
  AssignTeacherDto,
  ClassRosterDto,
  ClassSummaryDto,
  CreateClassDto,
  CreateSchoolYearDto,
  InitializeBatchClassesDto,
  PublishPlacementResultDto,
  RosterStudentItemDto,
  SchoolYearDto,
  TeacherOptionDto,
  UpdateClassDto,
} from '../../types/class.types';

export const classApi = {
  // Academic Years
  async getSchoolYears(): Promise<ApiResponse<SchoolYearDto[]>> {
    const res = await apiClient.get<ApiResponse<SchoolYearDto[]>>('/classes/school-years');
    return res.data;
  },

  async getActiveSchoolYear(): Promise<ApiResponse<SchoolYearDto>> {
    const res = await apiClient.get<ApiResponse<SchoolYearDto>>('/classes/school-years/active');
    return res.data;
  },

  async createSchoolYear(payload: CreateSchoolYearDto): Promise<ApiResponse<SchoolYearDto>> {
    const res = await apiClient.post<ApiResponse<SchoolYearDto>>('/classes/school-years', payload);
    return res.data;
  },

  async activateSchoolYear(id: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.put<ApiResponse<boolean>>(`/classes/school-years/${id}/activate`);
    return res.data;
  },

  // Classes
  async getClasses(
    schoolYearId?: number,
    grade?: number,
    status?: string,
  ): Promise<ApiResponse<ClassSummaryDto[]>> {
    const res = await apiClient.get<ApiResponse<ClassSummaryDto[]>>('/classes', {
      params: { schoolYearId, grade, status },
    });
    return res.data;
  },

  async getClassDetails(id: number): Promise<ApiResponse<ClassSummaryDto>> {
    const res = await apiClient.get<ApiResponse<ClassSummaryDto>>(`/classes/${id}`);
    return res.data;
  },

  async createClass(payload: CreateClassDto): Promise<ApiResponse<ClassSummaryDto>> {
    const res = await apiClient.post<ApiResponse<ClassSummaryDto>>('/classes', payload);
    return res.data;
  },

  async initializeBatch(payload: InitializeBatchClassesDto): Promise<ApiResponse<ClassSummaryDto[]>> {
    const res = await apiClient.post<ApiResponse<ClassSummaryDto[]>>('/classes/initialize-batch', payload);
    return res.data;
  },

  async updateClass(id: number, payload: UpdateClassDto): Promise<ApiResponse<ClassSummaryDto>> {
    const res = await apiClient.put<ApiResponse<ClassSummaryDto>>(`/classes/${id}`, payload);
    return res.data;
  },

  // Homeroom Teacher
  async getEligibleTeachers(schoolYearId?: number): Promise<ApiResponse<TeacherOptionDto[]>> {
    const res = await apiClient.get<ApiResponse<TeacherOptionDto[]>>('/classes/eligible-teachers', {
      params: { schoolYearId },
    });
    return res.data;
  },

  async assignHomeroomTeacher(classId: number, teacherUserId: number, staffProfileId?: number): Promise<ApiResponse<boolean>> {
    const payload: AssignTeacherDto = { 
      teacherUserId,
      staffProfileId: staffProfileId ?? teacherUserId
    };
    const res = await apiClient.post<ApiResponse<boolean>>(`/classes/${classId}/assign-homeroom-teacher`, payload);
    return res.data;
  },

  // Student Placement
  async getEligibleStudents(classId: number): Promise<ApiResponse<RosterStudentItemDto[]>> {
    const res = await apiClient.get<ApiResponse<RosterStudentItemDto[]>>(`/classes/${classId}/eligible-students`);
    return res.data;
  },

  async assignStudents(classId: number, studentIds: number[]): Promise<ApiResponse<boolean>> {
    const payload: AssignStudentsDto = { studentIds };
    const res = await apiClient.post<ApiResponse<boolean>>(`/classes/${classId}/assign-students`, payload);
    return res.data;
  },

  async removeStudent(classId: number, studentId: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/classes/${classId}/remove-student/${studentId}`);
    return res.data;
  },

  // Publishing
  async publishPlacement(classId: number): Promise<ApiResponse<PublishPlacementResultDto>> {
    const res = await apiClient.post<ApiResponse<PublishPlacementResultDto>>(`/classes/${classId}/publish-placement`);
    return res.data;
  },

  async publishAllPlacements(schoolYearId: number): Promise<ApiResponse<PublishPlacementResultDto[]>> {
    const res = await apiClient.post<ApiResponse<PublishPlacementResultDto[]>>(
      '/classes/publish-all-placements',
      null,
      { params: { schoolYearId } },
    );
    return res.data;
  },

  // Class Roster
  async getClassRoster(classId: number): Promise<ApiResponse<ClassRosterDto>> {
    const res = await apiClient.get<ApiResponse<ClassRosterDto>>(`/classes/${classId}/roster`);
    return res.data;
  },

  async getMyHomeroomClass(): Promise<ApiResponse<ClassRosterDto>> {
    const res = await apiClient.get<ApiResponse<ClassRosterDto>>('/classes/my-homeroom-class');
    return res.data;
  },
};
