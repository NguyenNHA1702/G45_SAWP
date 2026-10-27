import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  ClassPostDto,
  CreateClassPostDto,
  CreatePostCommentDto,
  PostCommentDto,
  StudentEvaluationDto,
  CreateStudentEvaluationDto,
  AcknowledgeEvaluationDto,
  StudentEvaluationRosterItemDto,
} from '../../types/contactBook.types';

export const contactBookApi = {
  // --- Bảng tin lớp học (UC-61, UC-62) ---
  getClassPosts: async (classId: number): Promise<ApiResponse<ClassPostDto[]>> => {
    const res = await apiClient.get<ApiResponse<ClassPostDto[]>>(`/contact-book/classes/${classId}/posts`);
    return res.data;
  },

  createClassPost: async (payload: CreateClassPostDto): Promise<ApiResponse<ClassPostDto>> => {
    const res = await apiClient.post<ApiResponse<ClassPostDto>>('/contact-book/posts', payload);
    return res.data;
  },

  togglePinPost: async (postId: number): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/contact-book/posts/${postId}/pin`);
    return res.data;
  },

  deleteClassPost: async (postId: number): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/contact-book/posts/${postId}`);
    return res.data;
  },

  addComment: async (postId: number, payload: CreatePostCommentDto): Promise<ApiResponse<PostCommentDto>> => {
    const res = await apiClient.post<ApiResponse<PostCommentDto>>(`/contact-book/posts/${postId}/comments`, payload);
    return res.data;
  },

  deleteComment: async (commentId: number): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/contact-book/comments/${commentId}`);
    return res.data;
  },

  toggleReaction: async (postId: number, reactionType = 'Heart'): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/contact-book/posts/${postId}/reactions`, { reactionType });
    return res.data;
  },

  // --- Sổ nhận xét học sinh (UC-63, UC-64) ---
  getClassEvaluationsRoster: async (classId: number, weekNumber = 1): Promise<ApiResponse<StudentEvaluationRosterItemDto[]>> => {
    const res = await apiClient.get<ApiResponse<StudentEvaluationRosterItemDto[]>>(`/contact-book/classes/${classId}/evaluations/roster`, {
      params: { weekNumber },
    });
    return res.data;
  },

  getStudentEvaluations: async (studentId: number): Promise<ApiResponse<StudentEvaluationDto[]>> => {
    const res = await apiClient.get<ApiResponse<StudentEvaluationDto[]>>(`/contact-book/students/${studentId}/evaluations`);
    return res.data;
  },

  createOrUpdateEvaluation: async (payload: CreateStudentEvaluationDto): Promise<ApiResponse<StudentEvaluationDto>> => {
    const res = await apiClient.post<ApiResponse<StudentEvaluationDto>>('/contact-book/evaluations', payload);
    return res.data;
  },

  acknowledgeEvaluation: async (id: number, payload: AcknowledgeEvaluationDto): Promise<ApiResponse<StudentEvaluationDto>> => {
    const res = await apiClient.post<ApiResponse<StudentEvaluationDto>>(`/contact-book/evaluations/${id}/acknowledge`, payload);
    return res.data;
  },
};
