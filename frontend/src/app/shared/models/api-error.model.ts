export interface ApiError {
  error?: string;
  title?: string;
  detail?: string;
}

export function extractApiErrorMessage(error: unknown): string {
  if (typeof error === 'object' && error !== null) {
    const apiError = error as ApiError;
    if (apiError.error) {
      return apiError.error;
    }
    if (apiError.detail) {
      return apiError.detail;
    }
    if (apiError.title) {
      return apiError.title;
    }
  }

  return 'Ocorreu um erro inesperado. Tente novamente.';
}
