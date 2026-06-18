import type { CreateHoldRequest, Hold, InventoryItem } from '../types';

const API_BASE = '/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.detail || body.title || `HTTP ${response.status}`);
  }
  return response.json();
}

export const api = {
  async getInventory(): Promise<InventoryItem[]> {
    const res = await fetch(`${API_BASE}/inventory`);
    return handleResponse<InventoryItem[]>(res);
  },

  async createHold(request: CreateHoldRequest): Promise<Hold> {
    const res = await fetch(`${API_BASE}/holds`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    return handleResponse<Hold>(res);
  },

  async getActiveHolds(): Promise<Hold[]> {
    const res = await fetch(`${API_BASE}/holds`);
    return handleResponse<Hold[]>(res);
  },

  async getHold(holdId: string): Promise<Hold> {
    const res = await fetch(`${API_BASE}/holds/${holdId}`);
    return handleResponse<Hold>(res);
  },

  async releaseHold(holdId: string): Promise<void> {
    const res = await fetch(`${API_BASE}/holds/${holdId}`, {
      method: 'DELETE',
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new Error(body.detail || body.title || `HTTP ${res.status}`);
    }
  },
};
