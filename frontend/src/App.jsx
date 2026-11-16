import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { locales } from './i18n/locales';
import { AdminPortal } from './components/AdminPortal';
import { AuthView } from './components/AuthView';
import { DeleteAccountModal, PriceAnalyticsModal } from './components/Modals';
import { ToastList } from './components/ToastList';
import { UserPortal } from './components/UserPortal';

const validatePasswordRules = (pw) => {
  const length = pw.length >= 8;
  const upper = /[A-Z]/.test(pw);
  const lower = /[a-z]/.test(pw);
  const number = /[0-9]/.test(pw);
  return { length, upper, lower, number, allValid: length && upper && lower && number };
};

export default function App() {
  const [lang, setLang] = useState(() => localStorage.getItem('sh_lang') || 'TR');
  const t = useCallback((key) => locales[lang]?.[key] || locales.TR[key] || key, [lang]);

  useEffect(() => {
    document.documentElement.lang = lang === 'TR' ? 'tr' : 'en';
    localStorage.setItem('sh_lang', lang);
  }, [lang]);

  const toggleLang = useCallback(() => {
    setLang((prev) => (prev === 'TR' ? 'EN' : 'TR'));
  }, []);

  // Auth & Profile State
  const [token, setToken] = useState(() => localStorage.getItem('sh_token') || '');
  const [user, setUser] = useState(null);
  const [authMode, setAuthMode] = useState('login');
  const [authError, setAuthError] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  // Shared UI & Navigation State
  const [toasts, setToasts] = useState([]);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [activeView, setActiveView] = useState('dashboard');
  const [adminTab, setAdminTab] = useState('overview');
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [priceModal, setPriceModal] = useState(null);
  const [signalRStatus, setSignalRStatus] = useState('connected');

  // Hunt State
  const [url, setUrl] = useState('');
  const [eventName, setEventName] = useState('');
  const [maxPrice, setMaxPrice] = useState('');
  const [preferredCategory, setPreferredCategory] = useState('');
  const [autoReserve, setAutoReserve] = useState(false);
  const [submittingHunt, setSubmittingHunt] = useState(false);
  const [activeHunts, setActiveHunts] = useState([]);
  const [historyHunts, setHistoryHunts] = useState([]);
  const [huntSearch, setHuntSearch] = useState('');
  const [historyFilter, setHistoryFilter] = useState('ALL');
  const urlInputRef = useRef(null);

  // Notifications State
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifDropdownOpen, setNotifDropdownOpen] = useState(false);

  // Profile / Security / Billing / Prefs State
  const [profileFirstName, setProfileFirstName] = useState('');
  const [profileLastName, setProfileLastName] = useState('');
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [notifPrefs, setNotifPrefs] = useState({
    notifyEmail: true,
    notifyTelegram: true,
    notifyDiscord: true,
    telegramChatId: '',
    discordWebhookUrl: ''
  });
  const [savingNotifPrefs, setSavingNotifPrefs] = useState(false);
  const [billingPlans, setBillingPlans] = useState([]);
  const [billingStatus, setBillingStatus] = useState(null);
  const [billingCycle, setBillingCycle] = useState('Monthly');
  const [billingActionLoading, setBillingActionLoading] = useState(false);

  // Admin State
  const [adminOverview, setAdminOverview] = useState(null);
  const [adminUsers, setAdminUsers] = useState([]);
  const [adminUsersSearch, setAdminUsersSearch] = useState('');
  const [adminLiveEvents, setAdminLiveEvents] = useState([]);
  const [priceLookupUrl, setPriceLookupUrl] = useState('');
  const [priceAnalytics, setPriceAnalytics] = useState(null);
  const [priceHistory, setPriceHistory] = useState([]);

  const hubRef = useRef(null);

  const addToast = useCallback((title, message, type = 'info') => {
    const id = `${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
    setToasts((prev) => [...prev, { id, title, message, type }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((item) => item.id !== id));
    }, 4500);
  }, []);

  const dismissToast = useCallback((id) => {
    setToasts((prev) => prev.filter((item) => item.id !== id));
  }, []);

  const handleLogout = useCallback(() => {
    localStorage.removeItem('sh_token');
    setToken('');
    setUser(null);
    setActiveHunts([]);
    setHistoryHunts([]);
    setNotifications([]);
    setUnreadCount(0);
    setBillingStatus(null);
    setAdminOverview(null);
    setAdminUsers([]);
    setSignalRStatus('connected');
    if (hubRef.current) {
      hubRef.current.stop().catch(() => {});
      hubRef.current = null;
    }
  }, []);

  const authFetch = useCallback(
    async (endpoint, options = {}) => {
      const headers = {
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...(options.headers || {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      };

      const res = await fetch(endpoint, { ...options, headers });
      if (res.status === 401) {
        addToast(t('authError'), t('noSession'), 'error');
        handleLogout();
        throw new Error('Unauthorized');
      }
      if (res.status === 429) {
        addToast(t('rateLimit'), t('tooManyReq'), 'error');
        throw new Error('RateLimited');
      }
      return res;
    },
    [token, addToast, t, handleLogout]
  );

  // Keyboard shortcuts
  useEffect(() => {
    const onKeyDown = (e) => {
      if (e.key === 'Escape') {
        if (deleteModalOpen) {
          setDeleteModalOpen(false);
        } else if (priceModal) {
          setPriceModal(null);
        } else if (notifDropdownOpen) {
          setNotifDropdownOpen(false);
        } else if (mobileMenuOpen) {
          setMobileMenuOpen(false);
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        if (user && !user.isAdmin && activeView === 'dashboard') {
          e.preventDefault();
          urlInputRef.current?.focus();
        }
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [deleteModalOpen, priceModal, notifDropdownOpen, mobileMenuOpen, user, activeView]);

  const fetchUserData = useCallback(async () => {
    if (!token) return;
    try {
      const [huntsRes, historyRes, notifsRes, plansRes, billingRes] = await Promise.all([
        authFetch('/api/hunts/active'),
        authFetch('/api/hunts/history'),
        authFetch('/api/notifications?take=50'),
        fetch('/api/billing/plans'),
        authFetch('/api/billing/me')
      ]);

      if (huntsRes.ok) setActiveHunts(await huntsRes.json());
      if (historyRes.ok) setHistoryHunts(await historyRes.json());
      if (notifsRes.ok) {
        const data = await notifsRes.json();
        setNotifications(data.items || []);
        setUnreadCount(data.unreadCount ?? 0);
      }
      if (plansRes.ok) setBillingPlans(await plansRes.json());
      if (billingRes.ok) setBillingStatus(await billingRes.json());
    } catch {
      // Handled inside authFetch
    }
  }, [token, authFetch]);

  const fetchAdminOverview = useCallback(async () => {
    try {
      const res = await authFetch('/api/admin/dashboard/overview');
      if (res.ok) setAdminOverview(await res.json());
    } catch {
      // Handled inside authFetch
    }
  }, [authFetch]);

  const fetchAdminUsers = useCallback(async () => {
    try {
      const res = await authFetch('/api/admin/users');
      if (res.ok) setAdminUsers(await res.json());
    } catch {
      // Handled inside authFetch
    }
  }, [authFetch]);

  // Load current user profile when token changes
  useEffect(() => {
    if (!token) {
      setUser(null);
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        const res = await authFetch('/api/user/me');
        if (!res.ok) {
          handleLogout();
          return;
        }
        const profile = await res.json();
        if (cancelled) return;

        setUser(profile);
        setProfileFirstName(profile.firstName || '');
        setProfileLastName(profile.lastName || '');
        setNotifPrefs({
          notifyEmail: profile.notifyEmail ?? true,
          notifyTelegram: profile.notifyTelegram ?? true,
          notifyDiscord: profile.notifyDiscord ?? true,
          telegramChatId: profile.telegramChatId || '',
          discordWebhookUrl: profile.discordWebhookUrl || ''
        });
      } catch {
        // Already handled
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [token, authFetch, handleLogout]);

  // Fetch portal data once user is loaded
  useEffect(() => {
    if (!user) return;
    if (user.isAdmin) {
      fetchAdminOverview();
      fetchAdminUsers();
    } else {
      fetchUserData();
    }
  }, [user, fetchAdminOverview, fetchAdminUsers, fetchUserData]);

  // SignalR Hub connection
  const startSignalR = useCallback(() => {
    if (!token || !user) return;
    if (hubRef.current) {
      hubRef.current.stop().catch(() => {});
      hubRef.current = null;
    }

    const hubPath = user.isAdmin ? '/adminHub' : '/notificationHub';
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubPath, { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.onreconnecting(() => setSignalRStatus('reconnecting'));
    connection.onreconnected(() => setSignalRStatus('connected'));
    connection.onclose(() => setSignalRStatus('disconnected'));

    if (user.isAdmin) {
      connection.on('AdminSeatDiscovered', (payload) => {
        setAdminLiveEvents((prev) => [
          {
            id: `${Date.now()}_${Math.random()}`,
            title: t('liveSeatDetected'),
            detail: payload?.eventName || payload?.routeOrEventId || t('seatFoundShort'),
            time: new Date().toLocaleTimeString(lang === 'TR' ? 'tr-TR' : 'en-US')
          },
          ...prev.slice(0, 24)
        ]);
        fetchAdminOverview();
      });

      connection.on('AdminDlqReplayed', (count) => {
        setAdminLiveEvents((prev) => [
          {
            id: `${Date.now()}_${Math.random()}`,
            title: 'DLQ Replay',
            detail: `${count} ${t('dlqReplayedDetail')}`,
            time: new Date().toLocaleTimeString(lang === 'TR' ? 'tr-TR' : 'en-US')
          },
          ...prev.slice(0, 24)
        ]);
        fetchAdminOverview();
      });

      connection.on('AdminStatsUpdated', () => {
        fetchAdminOverview();
      });
    } else {
      connection.on('SeatFound', (data) => {
        addToast(t('seatFound'), data?.message || t('seatDetectedFallback'), 'success');
        fetchUserData();
      });
    }

    connection
      .start()
      .then(() => setSignalRStatus('connected'))
      .catch(() => setSignalRStatus('disconnected'));

    hubRef.current = connection;
  }, [token, user, addToast, t, lang, fetchAdminOverview, fetchUserData]);

  useEffect(() => {
    if (!token || !user) return;
    startSignalR();
    return () => {
      if (hubRef.current) {
        hubRef.current.stop().catch(() => {});
        hubRef.current = null;
      }
    };
  }, [token, user, startSignalR]);

  const handleAuthSubmit = async (e) => {
    e.preventDefault();
    setAuthError('');
    try {
      if (authMode === 'login') {
        const res = await fetch('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email, password })
        });
        const data = await res.json().catch(() => ({}));
        if (!res.ok || !data.token) {
          setAuthError(data.message || t('loginFailed'));
          return;
        }
        localStorage.setItem('sh_token', data.token);
        setToken(data.token);
        addToast(t('loginSuccess'), t('loginSuccessMsg'), 'success');
      } else {
        const res = await fetch('/api/auth/register', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ firstName, lastName, email, password })
        });
        const data = await res.json().catch(() => ({}));
        if (!res.ok) {
          setAuthError(data.message || t('opFailed'));
          return;
        }
        setAuthMode('login');
        setPassword('');
        addToast(t('regSuccess'), t('regSuccessMsg'), 'success');
      }
    } catch {
      setAuthError(t('connFailed'));
    }
  };

  const handleCreateHunt = async (e) => {
    e.preventDefault();
    setSubmittingHunt(true);
    try {
      const payload = {
        Url: url,
        TargetDate: new Date().toISOString(),
        Language: lang.toLowerCase(),
        TargetEventName: eventName.trim() || null,
        MaxPrice: maxPrice ? Number(maxPrice) : null,
        PreferredCategory: preferredCategory.trim() || null,
        AutoReserveEnabled: autoReserve
      };
      const res = await authFetch('/api/hunts', {
        method: 'POST',
        body: JSON.stringify(payload)
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), data.message || data.title || t('opFailed'), 'error');
        return;
      }
      setUrl('');
      setEventName('');
      setMaxPrice('');
      setPreferredCategory('');
      setAutoReserve(false);
      addToast(t('scanStartedTitle'), t('scanStartedMsg'), 'success');
      await fetchUserData();
    } catch {
      // Handled inside authFetch
    } finally {
      setSubmittingHunt(false);
    }
  };

  const handleCancelHunt = async (id) => {
    try {
      const res = await authFetch(`/api/hunts/cancel/${id}`, { method: 'POST' });
      if (res.ok) {
        addToast(t('success'), t('cancelledMsg'), 'info');
        await fetchUserData();
      } else {
        const data = await res.json().catch(() => ({}));
        addToast(t('error'), data.message || t('opFailed'), 'error');
      }
    } catch {
      // Handled
    }
  };

  const handleOpenPriceModal = async (eventUrl, title) => {
    setPriceModal({ title, url: eventUrl, loading: true, summary: null, history: [], notFound: false });
    try {
      const encoded = encodeURIComponent(eventUrl);
      const [sumRes, histRes] = await Promise.all([
        authFetch(`/api/price-analytics/${encoded}`),
        authFetch(`/api/price-analytics/${encoded}/history?days=30`)
      ]);

      if (!sumRes.ok) {
        setPriceModal((prev) => prev && { ...prev, loading: false, notFound: true });
        return;
      }

      const summary = await sumRes.json();
      const history = histRes.ok ? await histRes.json() : [];
      setPriceModal({ title, url: eventUrl, loading: false, summary, history, notFound: false });
    } catch {
      setPriceModal(null);
    }
  };

  const markNotifRead = async (id) => {
    try {
      const res = await authFetch(`/api/notifications/${id}/read`, { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
        setUnreadCount(data.unreadCount ?? 0);
      }
    } catch {
      // Handled
    }
  };

  const markAllNotifsRead = async () => {
    try {
      const res = await authFetch('/api/notifications/read-all', { method: 'POST' });
      if (res.ok) {
        setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
        setUnreadCount(0);
      }
    } catch {
      // Handled
    }
  };

  const clearReadNotifs = async () => {
    try {
      const res = await authFetch('/api/notifications/read', { method: 'DELETE' });
      if (res.ok) {
        setNotifications((prev) => prev.filter((n) => !n.isRead));
        addToast(t('success'), t('clearReadDone'), 'info');
      }
    } catch {
      // Handled
    }
  };

  const handleUpdateProfile = async (e) => {
    e.preventDefault();
    try {
      const res = await authFetch('/api/user/profile', {
        method: 'PUT',
        body: JSON.stringify({ firstName: profileFirstName, lastName: profileLastName })
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), data.message || t('opFailed'), 'error');
        return;
      }
      setUser((prev) => ({ ...prev, firstName: data.firstName, lastName: data.lastName }));
      addToast(t('success'), t('profileUpdatedMsg'), 'success');
    } catch {
      // Handled
    }
  };

  const handleChangePassword = async (e) => {
    e.preventDefault();
    try {
      const res = await authFetch('/api/user/change-password', {
        method: 'PUT',
        body: JSON.stringify({ oldPassword, newPassword })
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), data.message || t('opFailed'), 'error');
        return;
      }
      setOldPassword('');
      setNewPassword('');
      addToast(t('success'), t('passUpdatedMsg'), 'success');
    } catch {
      // Handled
    }
  };

  const handleSaveNotificationPrefs = async (e) => {
    e.preventDefault();
    setSavingNotifPrefs(true);
    try {
      const res = await authFetch('/api/user/notifications', {
        method: 'PUT',
        body: JSON.stringify(notifPrefs)
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), data.message || t('opFailed'), 'error');
        return;
      }
      addToast(t('success'), t('notifPrefsSavedMsg'), 'success');
    } catch {
      // Handled
    } finally {
      setSavingNotifPrefs(false);
    }
  };

  const handleUpgradePlan = async (targetTier) => {
    setBillingActionLoading(true);
    try {
      const res = await authFetch('/api/billing/checkout', {
        method: 'POST',
        body: JSON.stringify({
          targetTier,
          billingCycle,
          idempotencyKey: `ui_${user?.id}_${targetTier}_${billingCycle}_${Date.now()}`
        })
      });
      const session = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), session.message || t('opFailed'), 'error');
        return;
      }

      if (session.provider === 'Sandbox' && session.externalSessionId) {
        const confirmRes = await authFetch(`/api/billing/sandbox/confirm/${session.externalSessionId}`, {
          method: 'POST'
        });
        const confirmData = await confirmRes.json().catch(() => ({}));
        if (!confirmRes.ok) {
          addToast(t('error'), confirmData.message || t('opFailed'), 'error');
          return;
        }
        if (confirmData.token) {
          localStorage.setItem('sh_token', confirmData.token);
          setToken(confirmData.token);
        }
        setUser((prev) => ({
          ...prev,
          tier: confirmData.effectiveTier || targetTier,
          subscriptionExpiresAtUtc: confirmData.subscriptionExpiresAtUtc
        }));
        addToast(t('planUpgradedTitle'), confirmData.message || t('success'), 'success');
        await fetchUserData();
      } else if (session.checkoutUrl) {
        window.location.href = session.checkoutUrl;
      }
    } catch {
      // Handled
    } finally {
      setBillingActionLoading(false);
    }
  };

  const handleCancelAutoRenew = async () => {
    setBillingActionLoading(true);
    try {
      const res = await authFetch('/api/billing/cancel', { method: 'POST' });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        addToast(t('error'), data.message || t('opFailed'), 'error');
        return;
      }
      addToast(t('autoRenewCancelledTitle'), data.message || t('success'), 'info');
      await fetchUserData();
    } catch {
      // Handled
    } finally {
      setBillingActionLoading(false);
    }
  };

  const handleDeleteAccount = async () => {
    try {
      const res = await authFetch('/api/user/account', { method: 'DELETE' });
      if (res.ok) {
        setDeleteModalOpen(false);
        addToast(t('success'), t('accDeleted'), 'info');
        handleLogout();
      }
    } catch {
      // Handled
    }
  };

  // Admin Handlers
  const handleUpdateUserTier = async (id, tier) => {
    try {
      const res = await authFetch(`/api/admin/users/${id}/tier`, {
        method: 'PUT',
        body: JSON.stringify({ tier })
      });
      const data = await res.json().catch(() => ({}));
      if (res.ok) {
        addToast(t('planUpdatedTitle'), data.message || t('success'), 'success');
        fetchAdminUsers();
        fetchAdminOverview();
      } else {
        addToast(t('planUpdateFailedTitle'), data.message || t('opFailed'), 'error');
      }
    } catch {
      // Handled
    }
  };

  const handleToggleUserStatus = async (id) => {
    try {
      const res = await authFetch(`/api/admin/users/${id}/toggle-status`, { method: 'POST' });
      const data = await res.json().catch(() => ({}));
      if (res.ok) {
        addToast(t('userStatusTitle'), data.message || t('success'), 'info');
        fetchAdminUsers();
      }
    } catch {
      // Handled
    }
  };

  const handleReplayDlq = async () => {
    try {
      const res = await authFetch('/api/admin/dlq/replay?maxCount=100', { method: 'POST' });
      const data = await res.json().catch(() => ({}));
      if (res.ok) {
        addToast('DLQ', `${data.replayedCount ?? 0} ${t('dlqReplayedToast')}`, 'success');
        fetchAdminOverview();
      }
    } catch {
      // Handled
    }
  };

  const handlePurgeDlq = async () => {
    try {
      const res = await authFetch('/api/admin/dlq/purge', { method: 'DELETE' });
      const data = await res.json().catch(() => ({}));
      if (res.ok) {
        addToast(t('dlqPurgedToastTitle'), data.message || t('success'), 'info');
        fetchAdminOverview();
      }
    } catch {
      // Handled
    }
  };

  const handleLookupPrice = async (e) => {
    e.preventDefault();
    if (!priceLookupUrl.trim()) return;
    try {
      const encoded = encodeURIComponent(priceLookupUrl.trim());
      const [sumRes, histRes] = await Promise.all([
        authFetch(`/api/price-analytics/${encoded}`),
        authFetch(`/api/price-analytics/${encoded}/history?days=30`)
      ]);
      if (!sumRes.ok) {
        setPriceAnalytics({ notFound: true });
        setPriceHistory([]);
        return;
      }
      setPriceAnalytics(await sumRes.json());
      setPriceHistory(histRes.ok ? await histRes.json() : []);
    } catch {
      // Handled
    }
  };

  const passwordValidations = useMemo(() => validatePasswordRules(password), [password]);
  const newPasswordValidations = useMemo(() => validatePasswordRules(newPassword), [newPassword]);

  const profileProps = {
    profileFirstName,
    setProfileFirstName,
    profileLastName,
    setProfileLastName,
    handleUpdateProfile,
    oldPassword,
    setOldPassword,
    newPassword,
    setNewPassword,
    newPasswordValidations,
    handleChangePassword
  };

  return (
    <>
      {!token || !user ? (
        <AuthView
          lang={lang}
          toggleLang={toggleLang}
          t={t}
          authMode={authMode}
          setAuthMode={setAuthMode}
          authError={authError}
          setAuthError={setAuthError}
          firstName={firstName}
          setFirstName={setFirstName}
          lastName={lastName}
          setLastName={setLastName}
          email={email}
          setEmail={setEmail}
          password={password}
          setPassword={setPassword}
          passwordValidations={passwordValidations}
          handleAuthSubmit={handleAuthSubmit}
          toasts={toasts}
        />
      ) : user.isAdmin ? (
        <AdminPortal
          user={user}
          lang={lang}
          toggleLang={toggleLang}
          t={t}
          adminTab={adminTab}
          setAdminTab={setAdminTab}
          mobileMenuOpen={mobileMenuOpen}
          setMobileMenuOpen={setMobileMenuOpen}
          adminOverview={adminOverview}
          adminUsers={adminUsers}
          adminUsersSearch={adminUsersSearch}
          setAdminUsersSearch={setAdminUsersSearch}
          adminLiveEvents={adminLiveEvents}
          priceLookupUrl={priceLookupUrl}
          setPriceLookupUrl={setPriceLookupUrl}
          priceAnalytics={priceAnalytics}
          priceHistory={priceHistory}
          signalRStatus={signalRStatus}
          onReconnectSignalR={startSignalR}
          fetchAdminOverview={fetchAdminOverview}
          fetchAdminUsers={fetchAdminUsers}
          handleUpdateUserTier={handleUpdateUserTier}
          handleToggleUserStatus={handleToggleUserStatus}
          handleReplayDlq={handleReplayDlq}
          handlePurgeDlq={handlePurgeDlq}
          handleLookupPrice={handleLookupPrice}
          handleLogout={handleLogout}
          profileProps={profileProps}
        />
      ) : (
        <UserPortal
          user={user}
          lang={lang}
          toggleLang={toggleLang}
          t={t}
          activeView={activeView}
          setActiveView={setActiveView}
          mobileMenuOpen={mobileMenuOpen}
          setMobileMenuOpen={setMobileMenuOpen}
          url={url}
          setUrl={setUrl}
          eventName={eventName}
          setEventName={setEventName}
          maxPrice={maxPrice}
          setMaxPrice={setMaxPrice}
          preferredCategory={preferredCategory}
          setPreferredCategory={setPreferredCategory}
          autoReserve={autoReserve}
          setAutoReserve={setAutoReserve}
          urlInputRef={urlInputRef}
          activeHunts={activeHunts}
          historyHunts={historyHunts}
          huntSearch={huntSearch}
          setHuntSearch={setHuntSearch}
          historyFilter={historyFilter}
          setHistoryFilter={setHistoryFilter}
          submittingHunt={submittingHunt}
          notifications={notifications}
          unreadCount={unreadCount}
          notifDropdownOpen={notifDropdownOpen}
          setNotifDropdownOpen={setNotifDropdownOpen}
          signalRStatus={signalRStatus}
          onReconnectSignalR={startSignalR}
          handleCreateHunt={handleCreateHunt}
          handleCancelHunt={handleCancelHunt}
          handleOpenPriceModal={handleOpenPriceModal}
          markNotifRead={markNotifRead}
          markAllNotifsRead={markAllNotifsRead}
          clearReadNotifs={clearReadNotifs}
          handleLogout={handleLogout}
          settingsProps={{
            billingPlans,
            billingStatus,
            billingCycle,
            setBillingCycle,
            billingActionLoading,
            handleUpgradePlan,
            handleCancelAutoRenew,
            notifPrefs,
            setNotifPrefs,
            savingNotifPrefs,
            handleSaveNotificationPrefs,
            onOpenDeleteModal: () => setDeleteModalOpen(true),
            profileProps
          }}
        />
      )}

      <PriceAnalyticsModal modal={priceModal} lang={lang} t={t} onClose={() => setPriceModal(null)} />
      <DeleteAccountModal
        open={deleteModalOpen}
        t={t}
        onClose={() => setDeleteModalOpen(false)}
        onConfirm={handleDeleteAccount}
      />
      <ToastList toasts={toasts} onDismiss={dismissToast} />
    </>
  );
}
