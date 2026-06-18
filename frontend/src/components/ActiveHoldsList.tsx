import { useEffect, useState } from 'react';
import type { Hold } from '../types';
import { HoldStatus } from '../types';
import { api } from '../api/client';

export function ActiveHoldsList({ refreshTrigger }: { refreshTrigger: number }) {
  const [holds, setHolds] = useState<Hold[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');
  const [releasing, setReleasing] = useState<string | null>(null);

  const fetchHolds = async (isRefresh = false) => {
    try {
      setError('');
      if (isRefresh) setRefreshing(true);
      else setLoading(true);
      const fetchedHolds = await api.getActiveHolds();
      setHolds(fetchedHolds);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load holds');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchHolds();
  }, [refreshTrigger]);

  const handleRelease = async (holdId: string) => {
    if (!window.confirm(`Release hold ${holdId.substring(0, 8)}...? Inventory will be restored.`)) {
      return;
    }
    setReleasing(holdId);
    try {
      await api.releaseHold(holdId);
      await fetchHolds();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to release hold');
    } finally {
      setReleasing(null);
    }
  };

  const statusLabel = (status: HoldStatus) => {
    switch (status) {
      case HoldStatus.Active: return 'Active';
      case HoldStatus.Released: return 'Released';
      case HoldStatus.Expired: return 'Expired';
      default: return 'Unknown';
    }
  };

  const statusClass = (status: HoldStatus) => {
    switch (status) {
      case HoldStatus.Active: return 'badge-success';
      case HoldStatus.Released: return 'badge-info';
      case HoldStatus.Expired: return 'badge-warning';
      default: return '';
    }
  };

  const formatDate = (s: string) => new Date(s).toLocaleString();

  if (loading && holds.length === 0) return <div className="loading">Loading holds...</div>;

  return (
    <div className="card">
      <div className="card-header">
        <h2>Active Holds</h2>
        <button className="btn btn-sm" onClick={() => fetchHolds(true)} disabled={refreshing}>{refreshing ? '...' : 'Refresh'}</button>
      </div>
      {error && <div className="error-banner">{error}</div>}
      {holds.length === 0 ? (
        <p className="text-muted" style={{ padding: '1rem' }}>No holds tracked. Create a hold to get started.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Hold ID</th>
              <th>Items</th>
              <th>Status</th>
              <th>Created</th>
              <th>Expires</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {holds.map((hold) => (
              <tr key={hold.holdId} className={hold.status !== HoldStatus.Active ? 'row-muted' : ''}>
                <td className="text-mono">{hold.holdId.substring(0, 8)}...</td>
                <td>
                  {hold.items.map((item, i) => (
                    <span key={i} className="item-tag">
                      {item.productName} &times; {item.quantity}
                    </span>
                  ))}
                </td>
                <td>
                  <span className={`badge ${statusClass(hold.status)}`}>
                    {statusLabel(hold.status)}
                  </span>
                </td>
                <td>{formatDate(hold.createdAt)}</td>
                <td>{formatDate(hold.expiresAt)}</td>
                <td>
                  {hold.status === HoldStatus.Active && (
                    <button
                      className="btn btn-danger btn-sm"
                      onClick={() => handleRelease(hold.holdId)}
                      disabled={releasing === hold.holdId}
                    >
                      {releasing === hold.holdId ? 'Releasing...' : 'Release'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
