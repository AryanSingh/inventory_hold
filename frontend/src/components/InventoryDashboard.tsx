import { useEffect, useState } from 'react';
import type { InventoryItem } from '../types';
import { api } from '../api/client';

export function InventoryDashboard() {
  const [inventory, setInventory] = useState<InventoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');

  const fetchInventory = async () => {
    try {
      setError('');
      if (inventory.length === 0) setLoading(true);
      else setRefreshing(true);
      const data = await api.getInventory();
      setInventory(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load inventory');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchInventory();
    const interval = setInterval(fetchInventory, 5000);
    return () => clearInterval(interval);
  }, []);

  if (loading && inventory.length === 0) return <div className="loading">Loading inventory...</div>;
  if (error) return <div className="error-banner">{error}</div>;

  return (
    <div className="card">
      <div className="card-header">
        <h2>
          Inventory Levels
          <span style={{
            animation: refreshing ? 'spin 1s linear infinite' : 'none',
            display: 'inline-block',
            marginLeft: '8px',
            opacity: refreshing ? 1 : 0,
            transition: 'opacity 0.2s'
          }}>↻</span>
        </h2>
        <button className="btn btn-sm" onClick={() => fetchInventory()} disabled={refreshing}>{refreshing ? '...' : 'Refresh'}</button>
      </div>
      <table>
        <thead>
          <tr>
            <th>Product</th>
            <th>SKU</th>
            <th>Available</th>
            <th>Reserved</th>
            <th>Total</th>
          </tr>
        </thead>
        <tbody>
          {inventory.map((item) => (
            <tr key={item.productId}>
              <td className="font-medium">{item.productName}</td>
              <td className="text-muted">{item.productId}</td>
              <td>
                <span className={`badge ${item.availableQuantity === 0 ? 'badge-danger' : item.availableQuantity < 5 ? 'badge-warning' : 'badge-success'}`}>
                  {item.availableQuantity}
                </span>
              </td>
              <td>
                <span className={`badge ${item.reservedQuantity > 0 ? 'badge-info' : ''}`}>
                  {item.reservedQuantity}
                </span>
              </td>
              <td>{item.totalQuantity}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
