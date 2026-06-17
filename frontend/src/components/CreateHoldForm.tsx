import { useState } from 'react';
import type { CreateHoldItemRequest, InventoryItem } from '../types';
import { api } from '../api/client';

interface Props {
  inventory: InventoryItem[];
  onHoldCreated: () => void;
}

export function CreateHoldForm({ inventory, onHoldCreated }: Props) {
  const [customerName, setCustomerName] = useState('');
  const [customerEmail, setCustomerEmail] = useState('');
  const [items, setItems] = useState<CreateHoldItemRequest[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const addItem = () => {
    setItems([...items, { productId: '', quantity: 1 }]);
  };

  const updateItem = (index: number, field: keyof CreateHoldItemRequest, value: string | number) => {
    const updated = [...items];
    if (field === 'quantity') {
      updated[index] = { ...updated[index], [field]: Math.max(1, Number(value)) };
    } else {
      updated[index] = { ...updated[index], [field]: String(value) };
    }
    setItems(updated);
  };

  const removeItem = (index: number) => {
    setItems(items.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (!customerName.trim() || !customerEmail.trim()) {
      setError('Customer name and email are required');
      return;
    }
    if (items.length === 0) {
      setError('Add at least one item');
      return;
    }
    if (items.some((i) => !i.productId)) {
      setError('Select a product for each item');
      return;
    }

    setLoading(true);
    try {
      await api.createHold({ customerName, customerEmail, items });
      setSuccess('Hold created successfully!');
      setCustomerName('');
      setCustomerEmail('');
      setItems([]);
      onHoldCreated();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create hold');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card">
      <div className="card-header">
        <h2>Create New Hold</h2>
      </div>
      <form onSubmit={handleSubmit}>
        {error && <div className="error-banner">{error}</div>}
        {success && <div className="success-banner">{success}</div>}

        <div className="form-row">
          <div className="form-group">
            <label>Customer Name</label>
            <input
              type="text"
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              placeholder="John Doe"
            />
          </div>
          <div className="form-group">
            <label>Customer Email</label>
            <input
              type="email"
              value={customerEmail}
              onChange={(e) => setCustomerEmail(e.target.value)}
              placeholder="john@example.com"
            />
          </div>
        </div>

        <div className="items-section">
          <h3>Items</h3>
          {items.map((item, index) => (
            <div key={index} className="item-row">
              <select
                value={item.productId}
                onChange={(e) => updateItem(index, 'productId', e.target.value)}
              >
                <option value="">Select product...</option>
                {inventory.map((inv) => (
                  <option key={inv.productId} value={inv.productId} disabled={inv.availableQuantity === 0}>
                    {inv.productName} ({inv.availableQuantity} available)
                  </option>
                ))}
              </select>
              <input
                type="number"
                min={1}
                value={item.quantity}
                onChange={(e) => updateItem(index, 'quantity', parseInt(e.target.value) || 1)}
                className="qty-input"
              />
              <button type="button" className="btn btn-danger btn-sm" onClick={() => removeItem(index)}>
                &times;
              </button>
            </div>
          ))}
          <button type="button" className="btn btn-secondary" onClick={addItem}>
            + Add Item
          </button>
        </div>

        <button type="submit" className="btn" disabled={loading}>
          {loading ? 'Creating...' : 'Create Hold'}
        </button>
      </form>
    </div>
  );
}
