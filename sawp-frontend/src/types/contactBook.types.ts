export interface PostAttachmentDto {
  id: number;
  fileUrl: string;
  fileName: string;
  fileType: string;
  fileSizeBytes: number;
}

export interface PostCommentDto {
  id: number;
  postId: number;
  userId: number;
  authorName: string;
  authorRoleLabel: string;
  content: string;
  createdAt: string;
  canDelete: boolean;
}

export interface ClassPostDto {
  id: number;
  classId: number;
  className: string;
  authorUserId: number;
  authorName: string;
  title: string;
  content: string;
  category: string; // 'General' | 'Homework' | 'Activity' | 'Welfare'
  isPinned: boolean;
  allowComments: boolean;
  createdAt: string;
  updatedAt?: string;
  reactionCount: number;
  userHasReacted: boolean;
  userReactionType?: string;
  attachments: PostAttachmentDto[];
  comments: PostCommentDto[];
}

export interface CreateClassPostDto {
  classId: number;
  title: string;
  content: string;
  category?: string;
  isPinned?: boolean;
  allowComments?: boolean;
  attachmentUrls?: string[];
}

export interface CreatePostCommentDto {
  content: string;
}

export interface ToggleReactionDto {
  reactionType: string;
}

export interface StudentEvaluationDto {
  id: number;
  studentId: number;
  studentName: string;
  studentCode: string;
  classId: number;
  className: string;
  teacherUserId: number;
  teacherName: string;
  evaluationDate: string;
  weekNumber: number;
  rating: string; // 'Excellent' | 'Good' | 'NeedsAttention'
  badges: string[];
  teacherNote: string;
  privateRemind?: string;
  isAcknowledged: boolean;
  acknowledgedAt?: string;
  acknowledgedByGuardianName?: string;
  parentFeedback?: string;
  createdAt: string;
}

export interface CreateStudentEvaluationDto {
  studentId: number;
  classId: number;
  evaluationDate: string;
  weekNumber: number;
  rating: string;
  badges: string[];
  teacherNote: string;
  privateRemind?: string;
}

export interface AcknowledgeEvaluationDto {
  parentFeedback?: string;
}

export interface StudentEvaluationRosterItemDto {
  studentId: number;
  studentName: string;
  studentCode: string;
  evaluation?: StudentEvaluationDto | null;
}
