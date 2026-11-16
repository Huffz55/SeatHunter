import { getDomainLabel } from '../utils/formatters';
import { ProfileSecurityCards } from './SettingsView';
import { SignalRBanner } from './SignalRBanner';

export function AdminPortal({
  user,
  lang,
  toggleLang,
  t,
  adminTab,
  setAdminTab,
  mobileMenuOpen,
  setMobileMenuOpen,
  adminOverview,
  adminUsers,
  adminUsersSearch,
  setAdminUsersSearch,
  adminLiveEvents,
  priceLookupUrl,
  setPriceLookupUrl,
  priceAnalytics,
  priceHistory,
  signalRStatus,
  onReconnectSignalR,
  fetchAdminOverview,
  fetchAdminUsers,
  handleUpdateUserTier,
  handleToggleUserStatus,
  handleReplayDlq,
  handlePurgeDlq,
  handleLookupPrice,
  handleLogout,
  profileProps
}) {
  const navItems = [
    ['overview', 'adminOverview', 'fa-chart-line'],
    ['infrastructure', 'adminInfra', 'fa-server'],
    ['users', 'adminUsers', 'fa-users-gear']
  ];

  const activeTitleKey =
    adminTab === 'overview'
      ? 'adminOverview'
      : adminTab === 'infrastructure'
      ? 'adminInfra'
      : adminTab === 'users'
      ? 'adminUsers'
      : 'adminSettings';

  const filteredUsers = (adminUsers || []).filter((u) => {
    const q = adminUsersSearch.trim().toLowerCase();
    if (!q) return true;
    return (
      (u.firstName || '').toLowerCase().includes(q) ||
      (u.lastName || '').toLowerCase().includes(q) ||
      (u.email || '').toLowerCase().includes(q) ||
      (u.tier || '').toLowerCase().includes(q)
    );
  });

  const hunts = adminOverview?.hunts || {};
  const usersStats = adminOverview?.users || {};
  const proxy = adminOverview?.proxyPool || {};
  const infra = adminOverview?.infrastructure || {};

  const totalHuntsCount = hunts.totalHunts || 0;
  const completedHuntsCount = hunts.completedHunts || 0;
  const successRate = totalHuntsCount > 0 ? ((completedHuntsCount / totalHuntsCount) * 100).toFixed(1) : '0.0';

  return (
    <div className="min-h-screen bg-[#070b14] text-slate-200 flex">
      {mobileMenuOpen && (
        <div
          className="fixed inset-0 bg-black/60 z-30 lg:hidden backdrop-blur-sm"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}

      {/* Admin Sidebar */}
      <aside
        className={`w-64 bg-[#0b101d] border-r border-white/[0.06] flex flex-col fixed inset-y-0 left-0 z-40 transition-transform duration-200 ${
          mobileMenuOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
        <div className="h-16 px-6 flex items-center justify-between border-b border-white/[0.06]">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-slate-900 border border-white/[0.08] flex items-center justify-center">
              <i className="fa-solid fa-crosshairs text-indigo-400 text-sm" />
            </div>
            <span className="font-bold text-white tracking-tight text-sm">
              Seat<span className="text-indigo-400">Hunter</span>
            </span>
          </div>
          <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-amber-500/15 text-amber-300 border border-amber-500/25">
            {t('adminBadge')}
          </span>
        </div>

        <nav className="flex-1 p-4 space-y-6 overflow-y-auto">
          <div>
            <div className="px-3 mb-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              {t('adminSectionMgmt')}
            </div>
            <div className="space-y-1">
              {navItems.map(([key, labelKey, icon]) => (
                <button
                  key={key}
                  onClick={() => {
                    setAdminTab(key);
                    setMobileMenuOpen(false);
                  }}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition-colors cursor-pointer ${
                    adminTab === key
                      ? 'bg-indigo-600/15 text-indigo-300 border border-indigo-500/25'
                      : 'text-slate-400 hover:text-slate-200 hover:bg-white/[0.03]'
                  }`}
                >
                  <i className={`fa-solid ${icon} w-4 text-center text-xs`} />
                  <span>{t(labelKey)}</span>
                </button>
              ))}
            </div>
          </div>

          <div>
            <div className="px-3 mb-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              {t('adminSectionAccount')}
            </div>
            <button
              onClick={() => {
                setAdminTab('settings');
                setMobileMenuOpen(false);
              }}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition-colors cursor-pointer ${
                adminTab === 'settings'
                  ? 'bg-indigo-600/15 text-indigo-300 border border-indigo-500/25'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-white/[0.03]'
              }`}
            >
              <i className="fa-solid fa-sliders w-4 text-center text-xs" />
              <span>{t('adminSettings')}</span>
            </button>
          </div>
        </nav>

        <div className="p-4 border-t border-white/[0.06] flex items-center justify-between gap-2">
          <div className="min-w-0">
            <div className="text-xs font-semibold text-white truncate">
              {user.firstName} {user.lastName}
            </div>
            <div className="text-[11px] text-slate-400 truncate">{user.email}</div>
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
            <h1 className="text-sm font-bold text-white truncate">{t(activeTitleKey)}</h1>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={() => {
                fetchAdminOverview();
                if (adminTab === 'users') fetchAdminUsers();
              }}
              className="text-slate-300 hover:text-white text-xs font-medium flex items-center gap-1.5 bg-slate-900/60 hover:bg-slate-800/80 px-3 py-1.5 rounded-lg border border-white/[0.08] cursor-pointer"
            >
              <i className="fa-solid fa-rotate text-[11px]" />
              <span className="hidden sm:inline">{t('refresh')}</span>
            </button>
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

        <main className="flex-1 p-6 lg:p-8 max-w-7xl w-full mx-auto space-y-6">
          {adminTab === 'overview' && (
            <div className="space-y-6 fade-enter">
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-5">
                  <div className="text-[11px] text-slate-400">{t('totalUsers')}</div>
                  <div className="text-2xl font-bold text-white mt-1.5">{usersStats.totalUsers ?? 0}</div>
                  <div className="text-[11px] text-slate-400 mt-2">
                    F: {usersStats.freeUsers ?? 0} • P: {usersStats.proUsers ?? 0} • V: {usersStats.vipUsers ?? 0}
                  </div>
                </div>

                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-5">
                  <div className="text-[11px] text-slate-400">{t('activeTotalHunts')}</div>
                  <div className="text-2xl font-bold text-white mt-1.5">
                    {hunts.activeHunts ?? 0}{' '}
                    <span className="text-sm font-normal text-slate-500">/ {totalHuntsCount}</span>
                  </div>
                  <div className="text-[11px] text-emerald-400 mt-2">
                    {t('foundLabel')}: {completedHuntsCount} ({successRate}% {t('successLabel')})
                  </div>
                </div>

                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-5">
                  <div className="text-[11px] text-slate-400">{t('proxyPoolHealth')}</div>
                  <div className="text-2xl font-bold text-emerald-400 mt-1.5">
                    %{proxy.healthPercentage ?? 100}
                  </div>
                  <div className="text-[11px] text-slate-400 mt-2">
                    {t('healthyLabel')}: {proxy.healthyProxies ?? 0} • {t('coolingDownLabel')}:{' '}
                    {proxy.coolingDownProxies ?? 0}
                  </div>
                </div>

                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-5">
                  <div className="text-[11px] text-slate-400">{t('queueDataVolume')}</div>
                  <div className="text-2xl font-bold text-white mt-1.5">
                    {infra.redisScheduledHuntsCount ?? 0}
                  </div>
                  <div className="text-[11px] text-slate-400 mt-2">
                    DLQ: <span className={infra.deadLetterQueueCount > 0 ? 'text-rose-400 font-semibold' : ''}>{infra.deadLetterQueueCount ?? 0}</span> • {t('priceDataLabel')}: {infra.totalPriceSnapshotsCount ?? 0}
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold text-white">{t('scanPerfTitle')}</h3>
                    <span className="text-[11px] text-slate-400">{t('liveDbSummary')}</span>
                  </div>
                  <div className="space-y-3 text-xs">
                    <div className="flex justify-between py-2 border-b border-white/[0.05]">
                      <span className="text-slate-400">{t('foundTicketsSuccess')}</span>
                      <span className="text-emerald-400 font-semibold">
                        {completedHuntsCount} ({successRate}%)
                      </span>
                    </div>
                    <div className="flex justify-between py-2 border-b border-white/[0.05]">
                      <span className="text-slate-400">{t('activeHuntsPlanLoad')}</span>
                      <span className="text-slate-200 font-medium">
                        {hunts.activeFreeHunts ?? 0} / {hunts.activeProHunts ?? 0} / {hunts.activeVipHunts ?? 0}
                      </span>
                    </div>
                    <div className="flex justify-between py-2 border-b border-white/[0.05]">
                      <span className="text-slate-400">{t('cancelledCountLabel')}</span>
                      <span className="text-slate-300">{hunts.cancelledHunts ?? 0}</span>
                    </div>
                    <div className="flex justify-between py-2">
                      <span className="text-slate-400">{t('expiredCountLabel')}</span>
                      <span className="text-slate-300">{hunts.expiredHunts ?? 0}</span>
                    </div>
                  </div>
                </div>

                <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-3">
                  <h3 className="text-sm font-semibold text-white">{t('liveSystemFeed')}</h3>
                  {adminLiveEvents.length === 0 ? (
                    <div className="py-10 text-center text-xs text-slate-500">{t('liveFeedPlaceholder')}</div>
                  ) : (
                    <div className="space-y-2 max-h-64 overflow-y-auto">
                      {adminLiveEvents.map((ev) => (
                        <div
                          key={ev.id}
                          className="p-3 rounded-lg bg-[#070b14] border border-white/[0.05] flex items-center justify-between gap-3 text-xs"
                        >
                          <div className="min-w-0">
                            <div className="font-semibold text-slate-200 truncate">{ev.title}</div>
                            <div className="text-[11px] text-slate-400 truncate mt-0.5">{ev.detail}</div>
                          </div>
                          <span className="text-[10px] font-mono text-slate-500 shrink-0">{ev.time}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {adminTab === 'infrastructure' && (
            <div className="space-y-6 fade-enter">
              <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div>
                    <h3 className="text-sm font-semibold text-white">{t('dlqTitle')}</h3>
                    <p className="text-xs text-slate-400 mt-1">{t('dlqDesc')}</p>
                  </div>
                  <span
                    className={`text-xs font-semibold px-3 py-1 rounded-full shrink-0 ${
                      (infra.deadLetterQueueCount ?? 0) > 0
                        ? 'bg-rose-500/15 text-rose-300 border border-rose-500/30'
                        : 'bg-emerald-500/10 text-emerald-300 border border-emerald-500/20'
                    }`}
                  >
                    {(infra.deadLetterQueueCount ?? 0) > 0
                      ? `${infra.deadLetterQueueCount} ${t('dlqWaiting')}`
                      : t('dlqClean')}
                  </span>
                </div>

                <div className="flex flex-wrap gap-3">
                  <button
                    onClick={handleReplayDlq}
                    className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition-colors cursor-pointer"
                  >
                    <i className="fa-solid fa-rotate-left mr-1.5" />
                    {t('dlqReplayBtn')}
                  </button>
                  <button
                    onClick={handlePurgeDlq}
                    className="px-4 py-2 rounded-lg bg-rose-500/15 hover:bg-rose-500/25 text-rose-300 border border-rose-500/30 text-xs font-semibold transition-colors cursor-pointer"
                  >
                    <i className="fa-regular fa-trash-can mr-1.5" />
                    {t('dlqPurgeBtn')}
                  </button>
                </div>
              </div>

              <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4">
                <h3 className="text-sm font-semibold text-white">{t('priceAnalyticsTitle')}</h3>
                <form onSubmit={handleLookupPrice} className="flex gap-2.5">
                  <input
                    type="text"
                    value={priceLookupUrl}
                    onChange={(e) => setPriceLookupUrl(e.target.value)}
                    placeholder={t('priceLookupPlaceholder')}
                    className="flex-1 bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3.5 py-2 text-xs outline-none focus:border-indigo-500/80"
                  />
                  <button
                    type="submit"
                    className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold cursor-pointer"
                  >
                    {t('lookupBtn')}
                  </button>
                </form>

                {priceAnalytics && (
                  <div className="space-y-3 pt-2">
                    {priceAnalytics.notFound ? (
                      <p className="text-xs text-slate-400">{t('noPriceHistory')}</p>
                    ) : (
                      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                        <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                          <div className="text-[10px] text-slate-400 uppercase">{t('currentPriceLabel')}</div>
                          <div className="text-sm font-bold text-white mt-1">{priceAnalytics.currentPrice} TL</div>
                        </div>
                        <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                          <div className="text-[10px] text-slate-400 uppercase">{t('minPriceLabel')}</div>
                          <div className="text-sm font-bold text-emerald-400 mt-1">{priceAnalytics.minPrice} TL</div>
                        </div>
                        <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                          <div className="text-[10px] text-slate-400 uppercase">{t('avgPriceLabel')}</div>
                          <div className="text-sm font-bold text-indigo-300 mt-1">{priceAnalytics.averagePrice} TL</div>
                        </div>
                        <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                          <div className="text-[10px] text-slate-400 uppercase">{t('maxObservedPriceLabel')}</div>
                          <div className="text-sm font-bold text-rose-400 mt-1">{priceAnalytics.maxPrice} TL</div>
                        </div>
                      </div>
                    )}
                    {priceHistory?.length > 0 && (
                      <div className="text-[11px] text-slate-400 font-mono">
                        {getDomainLabel(priceLookupUrl)} • {priceHistory.length} snapshots
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>
          )}

          {adminTab === 'users' && (
            <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-4 fade-enter">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <input
                  type="text"
                  value={adminUsersSearch}
                  onChange={(e) => setAdminUsersSearch(e.target.value)}
                  placeholder={t('adminSearchUsersPlaceholder')}
                  className="w-full sm:w-72 bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3.5 py-2 text-xs outline-none focus:border-indigo-500/80"
                />
                <span className="text-xs text-slate-400">
                  {t('totalPrefix')}: {filteredUsers.length} {t('totalUsersSuffix')}
                </span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead className="text-[10px] text-slate-500 uppercase border-b border-white/[0.06]">
                    <tr>
                      <th className="py-3 pr-4">{t('userCol')}</th>
                      <th className="py-3 pr-4">{t('roleCol')}</th>
                      <th className="py-3 pr-4">{t('scansCol')}</th>
                      <th className="py-3 pr-4">{t('changePlanCol')}</th>
                      <th className="py-3">{t('actionCol')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/[0.04]">
                    {filteredUsers.length === 0 ? (
                      <tr>
                        <td colSpan={5} className="py-8 text-center text-slate-500">
                          {t('noMatchingUsers')}
                        </td>
                      </tr>
                    ) : (
                      filteredUsers.map((u) => (
                        <tr key={u.id}>
                          <td className="py-3 pr-4">
                            <div className="font-semibold text-white">
                              {u.firstName} {u.lastName}
                            </div>
                            <div className="text-[11px] text-slate-400">{u.email}</div>
                          </td>
                          <td className="py-3 pr-4">
                            <span className="text-[11px] text-slate-300">{u.role || t('roleUser')}</span>
                          </td>
                          <td className="py-3 pr-4 font-mono text-[11px] text-slate-300">
                            {u.activeHuntCount} / {u.totalHuntCount}
                          </td>
                          <td className="py-3 pr-4">
                            <select
                              value={u.tier}
                              onChange={(e) => handleUpdateUserTier(u.id, e.target.value)}
                              className="bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-md px-2.5 py-1 text-xs outline-none cursor-pointer"
                            >
                              <option value="Free">Free</option>
                              <option value="Pro">Pro</option>
                              <option value="Vip">Vip</option>
                            </select>
                          </td>
                          <td className="py-3">
                            <button
                              onClick={() => handleToggleUserStatus(u.id)}
                              className={`px-3 py-1 rounded-md text-[11px] font-medium border cursor-pointer ${
                                u.isActive
                                  ? 'bg-rose-500/10 text-rose-300 border-rose-500/25 hover:bg-rose-500/20'
                                  : 'bg-emerald-500/10 text-emerald-300 border-emerald-500/25 hover:bg-emerald-500/20'
                              }`}
                            >
                              {u.isActive ? t('suspendBtn') : t('activateBtn')}
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {adminTab === 'settings' && (
            <div className="fade-enter">
              <ProfileSecurityCards {...profileProps} t={t} />
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
