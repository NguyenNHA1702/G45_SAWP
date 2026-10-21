import { apiClient } from '../apiClient';
import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  CreateStudentDto,
  DuplicateCheckResultDto,
  ImportStudentRowDto,
  ImportStudentsConfirmDto,
  ImportStudentsResultDto,
  ResolveDuplicateStudentDto,
  StudentFilterDto,
  StudentProfileDto,
  UpdateStudentDto,
} from '../../types/student.types';

export const studentApi = {
  async getStudents(filter: StudentFilterDto = {}): Promise<ApiResponse<PaginatedResult<StudentProfileDto>>> {
    const res = await apiClient.get<ApiResponse<PaginatedResult<StudentProfileDto>>>('/students', {
      params: filter,
    });
    return res.data;
  },

  async getStudentProfile(id: number): Promise<ApiResponse<StudentProfileDto>> {
    const res = await apiClient.get<ApiResponse<StudentProfileDto>>(`/students/${id}`);
    return res.data;
  },

  async createStudent(payload: CreateStudentDto): Promise<ApiResponse<StudentProfileDto>> {
    const res = await apiClient.post<ApiResponse<StudentProfileDto>>('/students', payload);
    return res.data;
  },

  async updateStudent(id: number, payload: UpdateStudentDto): Promise<ApiResponse<StudentProfileDto>> {
    const res = await apiClient.put<ApiResponse<StudentProfileDto>>(`/students/${id}`, payload);
    return res.data;
  },

  async activateStudent(id: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/students/${id}/activate`);
    return res.data;
  },

  async checkDuplicate(studentCode: string): Promise<ApiResponse<DuplicateCheckResultDto>> {
    const res = await apiClient.get<ApiResponse<DuplicateCheckResultDto>>('/students/check-duplicate', {
      params: { studentCode },
    });
    return res.data;
  },

  async resolveDuplicate(payload: ResolveDuplicateStudentDto): Promise<ApiResponse<StudentProfileDto>> {
    const res = await apiClient.post<ApiResponse<StudentProfileDto>>('/students/resolve-duplicate', payload);
    return res.data;
  },

  async parseExcel(file: File): Promise<ApiResponse<ImportStudentRowDto[]>> {
    const formData = new FormData();
    formData.append('file', file);
    const res = await apiClient.post<ApiResponse<ImportStudentRowDto[]>>('/students/import/parse', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return res.data;
  },

  async confirmImport(payload: ImportStudentsConfirmDto): Promise<ApiResponse<ImportStudentsResultDto>> {
    const res = await apiClient.post<ApiResponse<ImportStudentsResultDto>>('/students/import/confirm', payload);
    return res.data;
  },

  async downloadTemplate(): Promise<Blob> {
    const res = await apiClient.get('/students/import/template', {
      responseType: 'blob',
    });
    return res.data;
  },
};
