import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { extractApiErrorMessage } from '../../shared/models/api-error.model';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const message = extractApiErrorMessage(error.error ?? error);
      return throwError(() => ({ message, status: error.status }));
    }),
  );
};
