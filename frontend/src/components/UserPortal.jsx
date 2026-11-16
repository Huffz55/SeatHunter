import { formatNotifTime, getDomainLabel, localizeNotif } from '../utils/formatters';
import { NotificationDropdown } from './NotificationDropdown';
import { UserSettingsView } from './SettingsView';
import { SignalRBanner } from './SignalRBanner';

export function UserPortal({
  user,
  lang,
  toggleLang,
  t,
  activeView,
  setActiveView,
  mobileMenuOpen,
  setMobileMenuOpen,
  url,
  setUrl,
  eventName,
  setEventName,
  maxPrice,
  setMaxPrice,
  preferredCategory,
  setPreferredCategory,
  autoReserve,
  setAutoReserve,
  urlInputRef,
  activeHunts,
  historyHunts,
  huntSearch,
  setHuntSearch,
  historyFilter,
  setHistoryFilter,
  submittingHunt,
  notifications,
  unreadCount,
  notifDropdownOpen,
  setNotifDropdownOpen,
  signalRStatus,
  onReconnectSignalR,
  handleCreateHunt,
  handleCancelHunt,
  handleOpenPriceModal,
  markNotifRead,
  markAllNotifsRead,
  clearReadNotifs,
  handleLogout,
  settingsProps
}) {
  const isPro = user.tier === 'Pro' || user.tier === 'Vip';
  const isVip = user.tier === 'Vip';

  const visibleActiveHunts = (activeHunts || []).filter(
    (h) => h.status !== 'Cancelled' && h.status !== 'Expired'
  );

  const searchedActiveHunts = visibleActiveHunts.filter((h) => {
    const q = huntSearch.trim().toLowerCase();
    if (!q) return true;
    return (
      (h.eventName || '').toLowerCase().includes(q) ||
      (h.eventId || '').toLowerCase().includes(q) ||
      getDomainLabel(h.eventId || '').toLowerCase().includes(q)
    );
  });

  const filteredHistory = (historyHunts || []).filter((h) => {
    if (historyFilter === 'ALL') return true;
    return h.status === historyFilter;
  });

  const renderStatusBadge = (status) => {
    if (status === 'Found') {
      return (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-emerald-500/15 text-emerald-300 border border-emerald-500/25">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
          {t('found')}
        </span>
      );
    }
    if (status === 'Cancelled') {
      return (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-slate-800 text-slate-400 border border-white/[0.06]">
          {t('cancelled')}
        </span>
      );
    }
    if (status === 'Expired') {
      return (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-amber-500/15 text-amber-300 border border-amber-500/25">
          {t('expired')}
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold bg-indigo-500/15 text-indigo-300 border border-indigo-500/25">
        <span className="w-1.5 h-1.5 rounded-full bg-indigo-400 animate-pulse" />
        {t('scanning')}
      </span>
    );
  };

  const viewTitleKey =
    activeView === 'dashboard'
      ? 'dashboard'
      : activeView === 'hunts'
      ? 'activeScans'
      : activeView === 'notifications'
      ? 'notifications'
      : activeView === 'history'
      ? 'history'
      : 'settings';

  return (
    <div className="min-h-screen bg-[#070b14] text-slate-200 flex">
      {mobileMenuOpen && (
        <div
          className="fixed inset-0 bg-black/60 z-30 lg:hidden backdrop-blur-sm"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}

      {/* User Sidebar */}
      <aside
        className={`w-64 bg-[#0b101d] border-r border-white/[0.06] flex flex-col fixed inset-y-0 left-0 z-40 transition-transform duration-200 ${
          mobileMenuOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
        <div className="h-16 px-6 flex items-center gap-2.5 border-b border-white/[0.06]">
          <div className="w-8 h-8 rounded-lg bg-slate-900 border border-white/[0.08] flex items-center justify-center">
            <i className="fa-solid fa-crosshairs text-indigo-400 text-sm" />
          </div>
          <span className="font-bold text-white tracking-tight text-sm">
            Seat<span className="text-indigo-400">Hunter</span>
          </span>
        </div>

        <nav className="flex-1 p-4 space-y-6 overflow-y-auto">
          <div>
            <div className="px-3 mb-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              {t('menuSection')}
            </div>
            <div className="space-y-1">
              {[
                ['dashboard', 'dashboard', 'fa-compass', null],
                ['hunts', 'activeScans', 'fa-radar', visibleActiveHunts.length],
                ['notifications', 'notifications', 'fa-bell', unreadCount],
                ['history', 'history', 'fa-clock-rotate-left', null]
              ].map(([key, labelKey, icon, count]) => (
                <button
                  key={key}
                  onClick={() => {
                    setActiveView(key);
                    setMobileMenuOpen(false);
                  }}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-medium transition-colors cursor-pointer ${
                    activeView === key
                      ? 'bg-indigo-600/15 text-indigo-300 border border-indigo-500/25'
                      : 'text-slate-400 hover:text-slate-200 hover:bg-white/[0.03]'
                  }`}
                >
                  <span className="flex items-center gap-3">
                    <i className={`fa-solid ${icon} w-4 text-center text-xs`} />
                    <span>{t(labelKey)}</span>
                  </span>
                  {count !== null && count > 0 && (
                    <span className="px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-indigo-500/20 text-indigo-300">
                      {count}
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>

          <div>
            <div className="px-3 mb-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              {t('preferencesSection')}
            </div>
            <button
              onClick={() => {
                setActiveView('settings');
                setMobileMenuOpen(false);
              }}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition-colors cursor-pointer ${
                activeView === 'settings'
                  ? 'bg-indigo-600/15 text-indigo-300 border border-indigo-500/25'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-white/[0.03]'
              }`}
            >
              <i className="fa-solid fa-sliders w-4 text-center text-xs" />
              <span>{t('settings')}</span>
            </button>
          </div>
        </nav>

        <div className="p-4 border-t border-white/[0.06] flex items-center justify-between gap-2">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold text-white truncate">
                {user.firstName} {user.lastName}
              </span>
              <span
                className={`text-[9px] font-bold px-1.5 py-0.5 rounded ${
                  isVip
                    ? 'bg-amber-500/15 text-amber-300'
                    : isPro
                    ? 'bg-indigo-500/20 text-indigo-300'
                    : 'bg-slate-800 text-slate-400'
                }`}
              >
                {user.tier}
              </span>
            </div>
            <div className="text-[11px] text-slate-400 truncate mt-0.5">{user.email}</div>
          </div>
          <button
            onClick={handleLogout}
            className="text-slate-400 hover:text-rose-400 p-2 rounded-lg hover:bg-rose-500/10 transition-colors cursor-pointer"
            title={t('logout')}
          >
            <i className="fa-solid fa-arrow-right-from-bracket text-xs" />
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <div className="flex-1 lg:pl-64 flex flex-col min-w-0">
        <header className="h-16 px-6 lg:px-8 border-b border-white/[0.06] bg-[#070b14]/80 backdrop-blur-md sticky top-0 z-20 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3 min-w-0">
            <button
              onClick={() => setMobileMenuOpen(true)}
              className="lg:hidden text-slate-400 hover:text-white p-1.5 rounded-lg border border-white/[0.08]"
            >
              <i className="fa-solid fa-bars text-sm" />
            </button>
            <h1 className="text-sm font-bold text-white truncate">{t(viewTitleKey)}</h1>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={() => setActiveView('settings')}
              className={`hidden sm:inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-semibold transition-colors cursor-pointer ${
                isVip
                  ? 'bg-amber-500/10 border-amber-500/25 text-amber-300'
                  : isPro
                  ? 'bg-indigo-500/10 border-indigo-500/25 text-indigo-300'
                  : 'bg-slate-900/60 border-white/[0.08] text-slate-300 hover:text-white'
              }`}
            >
              {isVip && <i className="fa-solid fa-crown text-[10px]" />}
              <span>{user.tier} Plan</span>
            </button>

            <NotificationDropdown
              open={notifDropdownOpen}
              setOpen={setNotifDropdownOpen}
              notifications={notifications}
              unreadCount={unreadCount}
              lang={lang}
              t={t}
              markNotifRead={markNotifRead}
              markAllNotifsRead={markAllNotifsRead}
              clearReadNotifs={clearReadNotifs}
              onOpenAll={() => setActiveView('notifications')}
            />

            <button
              onClick={toggleLang}
              className="text-slate-400 hover:text-white transition-colors text-xs font-medium flex items-center gap-1.5 bg-slate-900/60 hover:bg-slate-800/80 px-3 py-1.5 rounded-lg border border-white/[0.08] cursor-pointer"
            >
              <i className="fa-solid fa-globe text-[11px]" />
              <span>{lang === 'TR' ? 'EN' : 'TR'}</span>
            </button>
          </div>
        </header>

        <SignalRBanner status={signalRStatus} onReconnect={onReconnectSignalR} t={t} />

        <main className="flex-1 p-6 lg:p-8 max-w-7xl w-full mx-auto">
          {activeView === 'dashboard' && (
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start fade-enter">
              {/* Left Column: New Hunt Form */}
              <div className="lg:col-span-5 bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 lg:sticky lg:top-20">
                <div className="flex items-center justify-between mb-5 pb-4 border-b border-white/[0.06]">
                  <div className="flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-lg bg-indigo-500/15 border border-indigo-500/25 flex items-center justify-center">
                      <i className="fa-solid fa-magnifying-glass text-indigo-400 text-xs" />
                    </div>
                    <h2 className="text-xs font-bold uppercase tracking-wider text-white">
                      {t('newSearch')}
                    </h2>
                  </div>
                  <span className="text-[11px] font-semibold text-indigo-300">{user.tier}</span>
                </div>

                <form onSubmit={handleCreateHunt} className="space-y-4">
                  <div>
                    <label className="block text-[11px] font-medium text-slate-300 mb-1.5">
                      {t('targetUrl')} <span className="text-rose-400">*</span>
                    </label>
                    <input
                      ref={urlInputRef}
                      type="url"
                      required
                      value={url}
                      onChange={(e) => setUrl(e.target.value)}
                      placeholder={t('urlPlaceholder')}
                      className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3.5 py-2.5 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                    />
                    <p className="text-[11px] text-slate-500 mt-1.5">{t('hintUrl')}</p>
                  </div>

                  <div>
                    <label className="block text-[11px] font-medium text-slate-300 mb-1.5">
                      {t('eventNameLabel')}{' '}
                      <span className="text-slate-500 font-normal">{t('optionalTag')}</span>
                    </label>
                    <input
                      type="text"
                      value={eventName}
                      onChange={(e) => setEventName(e.target.value)}
                      placeholder={t('eventNamePlaceholder')}
                      className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3.5 py-2.5 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                    />
                  </div>

                  {(isPro || isVip) && (
                    <div className="space-y-3 pt-1">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <label className="block text-[11px] font-medium text-slate-300 mb-1.5">
                            {t('maxPriceLabel')}
                          </label>
                          <input
                            type="number"
                            min="1"
                            step="any"
                            value={maxPrice}
                            onChange={(e) => setMaxPrice(e.target.value)}
                            placeholder={t('maxPricePlaceholder')}
                            className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                          />
                        </div>
                        <div>
                          <label className="block text-[11px] font-medium text-slate-300 mb-1.5">
                            {t('categoryLabel')}
                          </label>
                          <input
                            type="text"
                            value={preferredCategory}
                            onChange={(e) => setPreferredCategory(e.target.value)}
                            placeholder={t('categoryPlaceholder')}
                            className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                          />
                        </div>
                      </div>
                      <p className="text-[11px] text-slate-500">{t('categoryHint')}</p>
                    </div>
                  )}

                  {isVip && (
                    <label className="flex items-center justify-between gap-3 p-3.5 rounded-xl bg-amber-500/[0.06] border border-amber-500/25 cursor-pointer">
                      <div>
                        <div className="text-xs font-semibold text-white flex items-center gap-1.5">
                          <i className="fa-solid fa-crown text-amber-400 text-[11px]" />
                          <span>{t('autoReserveTitle')}</span>
                        </div>
                        <p className="text-[11px] text-slate-400 mt-0.5">{t('autoReserveDesc')}</p>
                      </div>
                      <input
                        type="checkbox"
                        checked={autoReserve}
                        onChange={(e) => setAutoReserve(e.target.checked)}
                        className="accent-amber-400 w-4 h-4 cursor-pointer shrink-0"
                      />
                    </label>
                  )}

                  <button
                    type="submit"
                    disabled={submittingHunt}
                    className="w-full bg-indigo-600 hover:bg-indigo-500 text-white py-2.5 px-4 rounded-lg font-semibold text-xs transition-all cursor-pointer disabled:opacity-50"
                  >
                    {submittingHunt ? t('starting') : t('startScan')}
                  </button>
                </form>
              </div>

              {/* Right Column: Active Hunts Summary */}
              <div className="lg:col-span-7 bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4">
                <div className="flex items-center justify-between border-b border-white/[0.06] pb-4">
                  <div className="flex items-center gap-2.5">
                    <span className="w-2 h-2 rounded-full bg-emerald-400" />
                    <h2 className="text-xs font-bold uppercase tracking-wider text-white">
                      {t('activeScans')}
                    </h2>
                    <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-slate-800 text-slate-300">
                      {visibleActiveHunts.length} {t('activeCountLabel')}
                    </span>
                  </div>
                  {visibleActiveHunts.length > 0 && (
                    <button
                      onClick={() => setActiveView('hunts')}
                      className="text-xs font-medium text-indigo-400 hover:text-indigo-300 cursor-pointer"
                    >
                      {t('manageAll')}
                    </button>
                  )}
                </div>

                {visibleActiveHunts.length === 0 ? (
                  <div className="py-12 px-4 text-center space-y-2.5">
                    <div className="w-11 h-11 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center mx-auto text-indigo-400">
                      <i className="fa-solid fa-radar text-base" />
                    </div>
                    <p className="text-xs font-semibold text-slate-200">{t('liveFeedEmptyTitle')}</p>
                    <p className="text-[11px] text-slate-400 max-w-sm mx-auto">{t('liveFeedEmptyDesc')}</p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {visibleActiveHunts.slice(0, 5).map((h) => (
                      <div
                        key={h.id}
                        className="p-4 rounded-xl bg-[#070b14]/70 border border-white/[0.05] flex flex-col sm:flex-row sm:items-center justify-between gap-3"
                      >
                        <div className="min-w-0 space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-slate-800 text-slate-300">
                              {getDomainLabel(h.eventId)}
                            </span>
                            <span className="text-xs font-semibold text-white truncate">
                              {h.eventName || getDomainLabel(h.eventId)}
                            </span>
                          </div>
                          <div className="text-[11px] font-mono text-slate-400 truncate">{h.eventId}</div>
                        </div>

                        <div className="flex items-center gap-2 shrink-0">
                          {renderStatusBadge(h.status)}
                          {(isPro || isVip) && (
                            <button
                              onClick={() => handleOpenPriceModal(h.eventId, h.eventName || getDomainLabel(h.eventId))}
                              className="px-2.5 py-1 rounded-lg bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-300 border border-indigo-500/20 text-[11px] font-medium cursor-pointer"
                            >
                              {t('priceInspectBtn')}
                            </button>
                          )}
                          <button
                            onClick={() => handleCancelHunt(h.id)}
                            className="px-2.5 py-1 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/20 text-[11px] font-medium cursor-pointer"
                          >
                            {t('cancel')}
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {activeView === 'hunts' && (
            <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4 fade-enter">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <input
                  type="text"
                  value={huntSearch}
                  onChange={(e) => setHuntSearch(e.target.value)}
                  placeholder={t('searchHuntsPlaceholder')}
                  className="w-full sm:w-80 bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3.5 py-2 text-xs outline-none focus:border-indigo-500/80"
                />
                <button
                  onClick={() => setActiveView('dashboard')}
                  className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold cursor-pointer shrink-0"
                >
                  {t('newSearch')}
                </button>
              </div>

              {searchedActiveHunts.length === 0 ? (
                <div className="py-12 text-center space-y-2">
                  <p className="text-xs font-semibold text-slate-300">{t('liveFeedEmptyTitle')}</p>
                  <button
                    onClick={() => setActiveView('dashboard')}
                    className="text-xs text-indigo-400 hover:text-indigo-300 font-medium cursor-pointer"
                  >
                    {t('startFirstHunt')}
                  </button>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="text-[10px] text-slate-500 uppercase border-b border-white/[0.06]">
                      <tr>
                        <th className="py-3 pr-4">{t('source')}</th>
                        <th className="py-3 pr-4">{t('routeId')}</th>
                        <th className="py-3 pr-4">{t('criteriaCol')}</th>
                        <th className="py-3 pr-4">{t('start')}</th>
                        <th className="py-3 pr-4">{t('status')}</th>
                        <th className="py-3">{t('actionCol')}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-white/[0.04]">
                      {searchedActiveHunts.map((h) => (
                        <tr key={h.id}>
                          <td className="py-3 pr-4 font-medium text-slate-300">{getDomainLabel(h.eventId)}</td>
                          <td className="py-3 pr-4 max-w-xs">
                            <div className="font-semibold text-white truncate">
                              {h.eventName || getDomainLabel(h.eventId)}
                            </div>
                            <div className="text-[11px] font-mono text-slate-400 truncate">{h.eventId}</div>
                          </td>
                          <td className="py-3 pr-4 text-[11px] text-slate-300">
                            {h.maxPrice ? `${t('maxShort')}: ${h.maxPrice} TL ` : ''}
                            {h.preferredCategory ? `• ${h.preferredCategory} ` : ''}
                            {h.autoReserveEnabled ? `• ${t('autoCartShort')}` : !h.maxPrice && !h.preferredCategory ? '—' : ''}
                          </td>
                          <td className="py-3 pr-4 font-mono text-[11px] text-slate-400">{h.startTime}</td>
                          <td className="py-3 pr-4">{renderStatusBadge(h.status)}</td>
                          <td className="py-3">
                            <div className="flex items-center gap-2">
                              {(isPro || isVip) && (
                                <button
                                  onClick={() => handleOpenPriceModal(h.eventId, h.eventName || getDomainLabel(h.eventId))}
                                  className="px-2.5 py-1 rounded-md bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-300 border border-indigo-500/20 text-[11px] font-medium cursor-pointer"
                                >
                                  {t('priceInspectBtn')}
                                </button>
                              )}
                              <button
                                onClick={() => handleCancelHunt(h.id)}
                                className="px-2.5 py-1 rounded-md bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/20 text-[11px] font-medium cursor-pointer"
                              >
                                {t('cancel')}
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {activeView === 'notifications' && (
            <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4 fade-enter">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-white/[0.06] pb-4">
                <div className="text-xs text-slate-300 font-medium">
                  {unreadCount > 0 ? `${unreadCount} ${t('unreadSuffix')}` : t('allNotifsRead')}
                </div>
                <div className="flex items-center gap-2">
                  {unreadCount > 0 && (
                    <button
                      onClick={markAllNotifsRead}
                      className="px-3 py-1.5 rounded-lg bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/25 text-xs font-medium cursor-pointer"
                    >
                      {t('markAllRead')}
                    </button>
                  )}
                  {notifications.some((n) => n.isRead) && (
                    <button
                      onClick={clearReadNotifs}
                      className="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-white/[0.08] text-xs font-medium cursor-pointer"
                    >
                      {t('clearRead')}
                    </button>
                  )}
                </div>
              </div>

              {notifications.length === 0 ? (
                <div className="py-12 text-center space-y-2">
                  <i className="fa-regular fa-bell-slash text-slate-600 text-xl" />
                  <p className="text-xs font-semibold text-slate-300">{t('noNotificationsTitle')}</p>
                  <p className="text-[11px] text-slate-500">{t('noNotificationsDesc')}</p>
                </div>
              ) : (
                <div className="space-y-2.5">
                  {notifications.map((n) => {
                    const { title, message } = localizeNotif(n, lang);
                    return (
                      <div
                        key={n.id}
                        className={`p-4 rounded-xl border flex flex-col sm:flex-row sm:items-center justify-between gap-3 ${
                          !n.isRead
                            ? 'bg-indigo-500/[0.06] border-indigo-500/25'
                            : 'bg-[#070b14]/60 border-white/[0.05]'
                        }`}
                      >
                        <div className="space-y-1 min-w-0">
                          <div className="flex items-center gap-2">
                            {!n.isRead && <span className="w-2 h-2 rounded-full bg-indigo-400 shrink-0" />}
                            <span className="text-xs font-semibold text-white">{title}</span>
                            <span className="text-[10px] font-mono text-slate-500">
                              {formatNotifTime(n.createdAt, lang)}
                            </span>
                          </div>
                          <p className="text-xs text-slate-300">{message}</p>
                        </div>

                        <div className="flex items-center gap-2 shrink-0">
                          {n.relatedUrl && (
                            <a
                              href={n.relatedUrl}
                              target="_blank"
                              rel="noreferrer"
                              className="px-3 py-1.5 rounded-lg bg-emerald-500/15 hover:bg-emerald-500/25 text-emerald-300 border border-emerald-500/25 text-[11px] font-semibold"
                            >
                              {t('goTicket')}
                            </a>
                          )}
                          {!n.isRead && (
                            <button
                              onClick={() => markNotifRead(n.id)}
                              className="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-white/[0.08] text-[11px] font-medium cursor-pointer"
                            >
                              {t('markRead')}
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {activeView === 'history' && (
            <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4 fade-enter">
              <div className="flex flex-wrap gap-2">
                {[
                  ['ALL', 'allFilter'],
                  ['Found', 'found'],
                  ['Cancelled', 'cancelled'],
                  ['Expired', 'expired']
                ].map(([statusKey, labelKey]) => (
                  <button
                    key={statusKey}
                    onClick={() => setHistoryFilter(statusKey)}
                    className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors cursor-pointer ${
                      historyFilter === statusKey
                        ? 'bg-indigo-600/20 text-indigo-300 border-indigo-500/30'
                        : 'bg-[#070b14] text-slate-400 border-white/[0.06] hover:text-white'
                    }`}
                  >
                    {t(labelKey)}
                  </button>
                ))}
              </div>

              {filteredHistory.length === 0 ? (
                <div className="py-12 text-center space-y-1.5">
                  <p className="text-xs font-semibold text-slate-300">{t('noHistoryTitle')}</p>
                  <p className="text-[11px] text-slate-500">{t('noHistory')}</p>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="text-[10px] text-slate-500 uppercase border-b border-white/[0.06]">
                      <tr>
                        <th className="py-3 pr-4">{t('source')}</th>
                        <th className="py-3 pr-4">{t('routeId')}</th>
                        <th className="py-3 pr-4">{t('dateCol')}</th>
                        <th className="py-3 pr-4">{t('status')}</th>
                        {(isPro || isVip) && <th className="py-3">{t('actionCol')}</th>}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-white/[0.04]">
                      {filteredHistory.map((h) => (
                        <tr key={h.id}>
                          <td className="py-3 pr-4 font-medium text-slate-300">{getDomainLabel(h.eventId)}</td>
                          <td className="py-3 pr-4 max-w-xs">
                            <div className="font-semibold text-white truncate">
                              {h.eventName || getDomainLabel(h.eventId)}
                            </div>
                            <div className="text-[11px] font-mono text-slate-400 truncate">{h.eventId}</div>
                          </td>
                          <td className="py-3 pr-4 font-mono text-[11px] text-slate-400">{h.date}</td>
                          <td className="py-3 pr-4">{renderStatusBadge(h.status)}</td>
                          {(isPro || isVip) && (
                            <td className="py-3">
                              <button
                                onClick={() => handleOpenPriceModal(h.eventId, h.eventName || getDomainLabel(h.eventId))}
                                className="px-2.5 py-1 rounded-md bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-300 border border-indigo-500/20 text-[11px] font-medium cursor-pointer"
                              >
                                {t('priceInspectBtn')}
                              </button>
                            </td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {activeView === 'settings' && <UserSettingsView {...settingsProps} user={user} lang={lang} t={t} />}
        </main>
      </div>
    </div>
  );
}
