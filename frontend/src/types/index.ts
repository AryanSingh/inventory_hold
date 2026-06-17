export interface InventoryItem {
  productId: string;
  productName: string;
  availableQuantity: number;
  reservedQuantity: number;
  totalQuantity: number;
  lastUpdated: string;
}

export interface HoldItem {
  productId: string;
  productName: string;
  quantity: number;
}

export interface Hold {
  holdId: string;
  customerName: string;
  customerEmail: string;
  items: HoldItem[];
  status: HoldStatus;
  createdAt: string;
  expiresAt: string;
}

export const HoldStatus = {
  Active: 0,
  Released: 1,
  Expired: 2,
} as const;
export type HoldStatus = typeof HoldStatus[keyof typeof HoldStatus];

export interface CreateHoldRequest {
  customerName: string;
  customerEmail: string;
  items: CreateHoldItemRequest[];
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
