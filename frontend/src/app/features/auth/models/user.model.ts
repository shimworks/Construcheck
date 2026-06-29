export type UserRole = 'Admin' | 'Viewer';

export interface User {
  id: string;
  email: string;
  roles: UserRole[];
}
