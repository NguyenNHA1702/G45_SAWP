export interface ApiResponse<T> {
  success: boolean;
  code: string | null;
  message: string;
  data: T | null;
  errors: string[] | null;
}

export interface PaginatedResult<T> {
  items: T[];
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
