import { apiClient } from '../apiClient';
import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  AssignStudentsDto,
  AssignTeacherDto,
  ClassRosterDto,
  ClassSummaryDto,
  CreateClassDto,
  CreateSchoolYearDto,
  RosterStudentItemDto,
  SchoolYearDto,
  TeacherOptionDto,
  UpdateClassDto,
} from '../../types/class.types';
import type {
  ClassImportMode,
  ClassImportPreviewDto,
  ClassImportResultDto,
  ClassImportRowsDto,
} from '../../types/classImport.types';

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

  async deleteSchoolYear(id: number, force = false): Promise<ApiResponse<boolean>> {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/classes/school-years/${id}`, {
      params: force ? { force: true } : undefined,
    });
    return res.data;
  },

  // UC-69 Import Class Assignments into a Draft school year
  async previewClassImport(schoolYearId: number, file: File, mode: ClassImportMode): Promise<ApiResponse<ClassImportPreviewDto>> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('mode', mode);
    const res = await apiClient.post<ApiResponse<ClassImportPreviewDto>>(
      `/classes/school-years/${schoolYearId}/import/preview`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 300000 },
    );
    return res.data;
  },

  async previewClassImportRows(schoolYearId: number, rows: ClassImportRowsDto): Promise<ApiResponse<ClassImportPreviewDto>> {
    const res = await apiClient.post<ApiResponse<ClassImportPreviewDto>>(
      `/classes/school-years/${schoolYearId}/import/preview-rows`,
      rows,
      { timeout: 300000 },
    );
    return res.data;
  },

  async confirmClassImport(schoolYearId: number, rows: ClassImportRowsDto): Promise<ApiResponse<ClassImportResultDto>> {
    const res = await apiClient.post<ApiResponse<ClassImportResultDto>>(
      `/classes/school-years/${schoolYearId}/import/confirm`,
      rows,
      { timeout: 300000 },
    );
    return res.data;
  },

  async downloadClassImportTemplate(): Promise<Blob> {
    const res = await apiClient.get('/classes/import/template', { responseType: 'blob' });
    return res.data;
  },

  // UC-14 A2: mark a class of a Draft school year as Reviewed
  async markClassReviewed(classId: number): Promise<ApiResponse<ClassSummaryDto>> {
    const res = await apiClient.post<ApiResponse<ClassSummaryDto>>(`/classes/${classId}/mark-reviewed`);
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

  async updateClass(id: number, payload: UpdateClassDto): Promise<ApiResponse<ClassSummaryDto>> {
    const res = await apiClient.put<ApiResponse<ClassSummaryDto>>(`/classes/${id}`, payload);
    return res.data;
  },

  async deleteClass(id: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/classes/${id}`);
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

  async unassignHomeroomTeacher(classId: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/classes/${classId}/unassign-homeroom-teacher`);
    return res.data;
  },

  // Student class assignment (UC-16)
  async getEligibleStudentsPaged(
    classId: number,
    search?: string,
    pageIndex = 1,
    pageSize = 25,
  ): Promise<ApiResponse<PaginatedResult<RosterStudentItemDto>>> {
    const res = await apiClient.get<ApiResponse<PaginatedResult<RosterStudentItemDto>>>(
      `/classes/${classId}/eligible-students/paged`,
      { params: { ...(search ? { search } : {}), pageIndex, pageSize } },
    );
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

  async removeStudentsBatch(classId: number, studentIds: number[]): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/classes/${classId}/remove-students-batch`, { studentIds });
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
