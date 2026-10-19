import axios from 'axios';
import type { ApiResponse } from '../types/common.types';
import { SYSTEM_MESSAGES, getSystemMessage } from '../constants/messages';

export class ApiError extends Error {
  code: string | null;
  errors: string[];

  constructor(message: string, code: string | null, errors: string[] = []) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.errors = errors;
  }
}

const fromResponse = (r: ApiResponse<unknown>): ApiError => {
  const dictionary = r.code ? getSystemMessage(r.code)?.content : undefined;
  return new ApiError(r.message || dictionary || SYSTEM_MESSAGES.MSG50.content, r.code, r.errors ?? []);
};

export const asApiError = (e: unknown): ApiError => {
  if (e instanceof ApiError) return e;
  if (axios.isAxiosError<ApiResponse<unknown>>(e) && e.response?.data && typeof e.response.data === 'object') {
    return fromResponse(e.response.data);
  }
  return new ApiError(SYSTEM_MESSAGES.MSG50.content, 'MSG50');
};

export const unwrap = async <T>(p: Promise<{ data: ApiResponse<T> }>): Promise<ApiResponse<T>> => {
  try {
    const res = await p;
    if (!res.data.success) throw fromResponse(res.data);
    return res.data;
  } catch (e) {
    throw asApiError(e);
  }
};

export const unwrapData = async <T>(p: Promise<{ data: ApiResponse<T> }>): Promise<T> => {
  const r = await unwrap(p);
  return r.data as T;
};
