export interface Category {
  id: string;
  name: string;
}

export interface CategoryDetail {
  id: string;
  name: string;
  description: string | null;
  isSystemDefined: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface CategoryStats {
  categoryId: string;
  categoryName: string;
  transactionCount: number;
  totalAmount: number;
  aiClassifiedPercent: number;
  humanCorrectedPercent: number;
}

export interface CreateCategoryRequest {
  name: string;
  description?: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  description?: string | null;
}
