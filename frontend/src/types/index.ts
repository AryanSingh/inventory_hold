export interface InventoryItem {
  productId: string;
  productName: string;
  availableQuantity: number;
  reservedQuantity: number;
  totalQuantity: number;
  updatedAt: string;
}

export interface HoldItem {
  productId: string;
  productName: string;
  quantity: number;
}

export interface Hold {
  holdId: string;
  items: HoldItem[];
  status: HoldStatus;
  createdAt: string;
  expiresAt: string;
  releasedAt?: string;
}

export const HoldStatus = {
  Active: 'Active',
  Released: 'Released',
  Expired: 'Expired',
} as const;
export type HoldStatus = typeof HoldStatus[keyof typeof HoldStatus];

export interface CreateHoldRequest {
  holdId?: string;
  items: CreateHoldItemRequest[];
  durationMinutes?: number;
}

export interface CreateHoldItemRequest {
  productId: string;
  quantity: number;
}

export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
