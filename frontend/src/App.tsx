import { useEffect, useState, useCallback } from 'react';
import type { InventoryItem } from './types';
import { api } from './api/client';
import { InventoryDashboard } from './components/InventoryDashboard';
import { CreateHoldForm } from './components/CreateHoldForm';
import { ActiveHoldsList } from './components/ActiveHoldsList';
import './App.css';

function App() {
  const [inventory, setInventory] = useState<InventoryItem[]>([]);
  const [holdRefreshTrigger, setHoldRefreshTrigger] = useState(0);

  const fetchInventory = useCallback(async () => {
    try {
      const data = await api.getInventory();
      setInventory(data);
    } catch {
      // silent - dashboard handles its own errors
    }
  }, []);

  useEffect(() => {
    fetchInventory();
  }, [fetchInventory, holdRefreshTrigger]);

  const handleHoldCreated = () => {
    fetchInventory();
    setHoldRefreshTrigger(prev => prev + 1);
  };

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-content">
          <h1>Inventory Hold Service</h1>
          <p className="subtitle">E-Commerce Checkout &middot; Inventory Management Dashboard</p>
        </div>
      </header>
      <main className="main-content">
        <div className="grid">
          <div className="col-full">
            <InventoryDashboard />
          </div>
          <div className="col-half">
            <CreateHoldForm inventory={inventory} onHoldCreated={handleHoldCreated} />
          </div>
          <div className="col-half">
            <ActiveHoldsList refreshTrigger={holdRefreshTrigger} />
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
