import { useState } from 'react';
import type { CreateHoldItemRequest, InventoryItem } from '../types';
import { api } from '../api/client';

interface Props {
  inventory: InventoryItem[];
  onHoldCreated: () => void;
}

export function CreateHoldForm({ inventory, onHoldCreated }: Props) {
  const [items, setItems] = useState<CreateHoldItemRequest[]>([]);
  const [durationMinutes, setDurationMinutes] = useState(15);
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
      await api.createHold({ items, durationMinutes });
      setSuccess('Hold created successfully!');
      setItems([]);
      setDurationMinutes(15);
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

        <div className="duration-section" style={{ marginTop: '1rem' }}>
          <label htmlFor="duration" style={{ display: 'block', marginBottom: '0.25rem', fontWeight: 500 }}>
            Hold Duration (minutes)
          </label>
          <input
            id="duration"
            type="number"
            min={1}
            max={1440}
            step={15}
            value={durationMinutes}
            onChange={(e) => setDurationMinutes(Math.max(1, Math.min(1440, parseInt(e.target.value) || 15)))}
            style={{ width: '120px', padding: '0.5rem', borderRadius: '4px', border: '1px solid #374151', background: '#1f2937', color: '#e5e7eb' }}
          />
          <small style={{ display: 'block', marginTop: '0.25rem', color: '#9ca3af', fontSize: '0.8rem' }}>
            Default: 15 min. Maximum: 24 hours (1440 min)
          </small>
        </div>

        <button type="submit" className="btn" disabled={loading} style={{ marginTop: '1rem' }}>
          {loading ? 'Creating...' : 'Create Hold'}
        </button>
      </form>
    </div>
  );
}
