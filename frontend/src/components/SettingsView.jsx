export function ProfileSecurityCards({
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
  handleChangePassword,
  t
}) {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6">
        <h3 className="text-sm font-semibold text-white mb-4 flex items-center gap-2">
          <i className="fa-regular fa-id-card text-indigo-400 text-xs" />
          <span>{t('profileInfoTitle')}</span>
        </h3>
        <form onSubmit={handleUpdateProfile} className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5">{t('name')}</label>
              <input
                type="text"
                value={profileFirstName}
                onChange={(e) => setProfileFirstName(e.target.value)}
                required
                className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5">{t('surname')}</label>
              <input
                type="text"
                value={profileLastName}
                onChange={(e) => setProfileLastName(e.target.value)}
                required
                className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
              />
            </div>
          </div>
          <button
            type="submit"
            className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition-colors cursor-pointer"
          >
            {t('updateProfile')}
          </button>
        </form>
      </div>

      <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6">
        <h3 className="text-sm font-semibold text-white mb-4 flex items-center gap-2">
          <i className="fa-solid fa-key text-indigo-400 text-xs" />
          <span>{t('changePass')}</span>
        </h3>
        <form onSubmit={handleChangePassword} className="space-y-3.5">
          <div>
            <label className="block text-[11px] font-medium text-slate-400 mb-1.5">{t('currentPass')}</label>
            <input
              type="password"
              value={oldPassword}
              onChange={(e) => setOldPassword(e.target.value)}
              required
              placeholder="••••••••"
              className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
            />
          </div>
          <div>
            <label className="block text-[11px] font-medium text-slate-400 mb-1.5">{t('newPass')}</label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              placeholder="••••••••"
              className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
            />
            {newPassword.length > 0 && (
              <div className="grid grid-cols-2 gap-1.5 pt-2 text-[10px]">
                <div className={newPasswordValidations.length ? 'text-emerald-400' : 'text-slate-500'}>
                  {t('pwMinLen')}
                </div>
                <div className={newPasswordValidations.upper ? 'text-emerald-400' : 'text-slate-500'}>
                  {t('pwUpper')}
                </div>
                <div className={newPasswordValidations.lower ? 'text-emerald-400' : 'text-slate-500'}>
                  {t('pwLower')}
                </div>
                <div className={newPasswordValidations.number ? 'text-emerald-400' : 'text-slate-500'}>
                  {t('pwNumber')}
                </div>
              </div>
            )}
          </div>
          <button
            type="submit"
            disabled={!newPasswordValidations.allValid}
            className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold border border-white/[0.08] disabled:opacity-50 transition-colors cursor-pointer"
          >
            {t('updatePass')}
          </button>
        </form>
      </div>
    </div>
  );
}

export function UserSettingsView({
  user,
  lang,
  t,
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
  onOpenDeleteModal,
  profileProps
}) {
  const effectiveTier = billingStatus?.effectiveTier || user?.tier || 'Free';

  return (
    <div className="space-y-6 fade-enter">
      {/* Plan & Billing Management */}
      <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 space-y-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h3 className="text-sm font-semibold text-white flex items-center gap-2">
              <i className="fa-solid fa-crown text-amber-400 text-xs" />
              <span>{t('billingTitle')}</span>
            </h3>
            {billingStatus?.subscriptionExpiresAtUtc && (
              <p className="text-[11px] text-slate-400 mt-1">
                {t('expiresOnLabel')}:{' '}
                <span className="text-slate-200 font-medium">
                  {new Date(billingStatus.subscriptionExpiresAtUtc).toLocaleDateString(
                    lang === 'TR' ? 'tr-TR' : 'en-US'
                  )}
                </span>
                {billingStatus?.activeSubscription?.cancelAtPeriodEnd && (
                  <span className="ml-2 text-amber-400">({t('autoRenewCancelledNote')})</span>
                )}
              </p>
            )}
          </div>

          <div className="flex items-center gap-2">
            <div className="inline-flex bg-[#070b14] p-1 rounded-lg border border-white/[0.08]">
              {['Monthly', 'Yearly'].map((cycle) => (
                <button
                  key={cycle}
                  type="button"
                  onClick={() => setBillingCycle(cycle)}
                  className={`px-3 py-1 rounded-md text-[11px] font-semibold transition-colors cursor-pointer ${
                    billingCycle === cycle
                      ? 'bg-indigo-600 text-white'
                      : 'text-slate-400 hover:text-white'
                  }`}
                >
                  {cycle === 'Monthly' ? t('billingCycleMonthly') : t('billingCycleYearly')}
                  {cycle === 'Yearly' && (
                    <span className="ml-1.5 text-[9px] px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-300">
                      {t('yearlyDiscountBadge')}
                    </span>
                  )}
                </button>
              ))}
            </div>

            {effectiveTier !== 'Free' &&
              billingStatus?.activeSubscription &&
              !billingStatus.activeSubscription.cancelAtPeriodEnd && (
                <button
                  type="button"
                  disabled={billingActionLoading}
                  onClick={handleCancelAutoRenew}
                  className="px-3 py-1.5 rounded-lg bg-slate-800/80 hover:bg-slate-800 border border-white/[0.08] text-xs text-slate-300 transition-colors cursor-pointer disabled:opacity-50"
                >
                  {t('autoRenewCancelBtn')}
                </button>
              )}
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {(billingPlans || []).map((plan) => {
            const isCurrent = effectiveTier === plan.tier;
            const price = billingCycle === 'Yearly' ? plan.yearlyPriceTry : plan.monthlyPriceTry;
            return (
              <div
                key={plan.tier}
                className={`rounded-xl p-5 border flex flex-col justify-between gap-4 ${
                  isCurrent
                    ? 'bg-indigo-500/10 border-indigo-500/40'
                    : 'bg-[#070b14]/70 border-white/[0.06]'
                }`}
              >
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-bold text-white">{plan.name}</span>
                    {isCurrent && (
                      <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                        {t('currentPlanBadge')}
                      </span>
                    )}
                  </div>
                  <div className="text-2xl font-bold text-white">
                    {price} <span className="text-xs font-normal text-slate-400">{plan.currency}</span>
                  </div>
                  <ul className="space-y-1.5 text-[11px] text-slate-300">
                    <li>
                      <i className="fa-solid fa-check text-emerald-400 mr-1.5" />
                      {plan.maxActiveHunts} {t('concurrentHuntsLabel')}
                    </li>
                    <li>
                      <i className="fa-solid fa-check text-emerald-400 mr-1.5" />
                      {plan.minScanIntervalMinutes} dk {t('scanSpeedLabel')}
                    </li>
                    {plan.allowAdvancedCriteria && (
                      <li>
                        <i className="fa-solid fa-check text-emerald-400 mr-1.5" />
                        {t('priceAnalyticsFeature')}
                      </li>
                    )}
                    {plan.allowAutoReservation && (
                      <li>
                        <i className="fa-solid fa-check text-amber-400 mr-1.5" />
                        {t('autoCartFeature')}
                      </li>
                    )}
                  </ul>
                </div>

                {plan.tier !== 'Free' && !isCurrent && (
                  <button
                    type="button"
                    disabled={billingActionLoading}
                    onClick={() => handleUpgradePlan(plan.tier)}
                    className="w-full py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50"
                  >
                    {t('upgradeToBtn')} ({plan.name})
                  </button>
                )}
              </div>
            );
          })}
        </div>

        <div className="border-t border-white/[0.06] pt-4">
          <h4 className="text-xs font-semibold text-slate-300 mb-3">{t('billingHistoryTitle')}</h4>
          {!billingStatus?.transactions?.length ? (
            <p className="text-[11px] text-slate-500">{t('noBillingHistory')}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="text-[10px] text-slate-500 uppercase border-b border-white/[0.06]">
                  <tr>
                    <th className="py-2 pr-4">{t('dateCol')}</th>
                    <th className="py-2 pr-4">{t('changePlanCol')}</th>
                    <th className="py-2 pr-4">{t('amountCol')}</th>
                    <th className="py-2">{t('status')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-white/[0.04]">
                  {billingStatus.transactions.slice(0, 6).map((tx) => (
                    <tr key={tx.id}>
                      <td className="py-2.5 pr-4 text-slate-400 font-mono text-[11px]">
                        {new Date(tx.createdAtUtc).toLocaleDateString(lang === 'TR' ? 'tr-TR' : 'en-US')}
                      </td>
                      <td className="py-2.5 pr-4 text-slate-200 font-medium">
                        {tx.targetTier} ({tx.cycle})
                      </td>
                      <td className="py-2.5 pr-4 text-slate-200">
                        {tx.amount} {tx.currency}
                      </td>
                      <td className="py-2.5">
                        <span
                          className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                            tx.status === 'Succeeded'
                              ? 'bg-emerald-500/10 text-emerald-400'
                              : tx.status === 'Pending'
                              ? 'bg-amber-500/10 text-amber-400'
                              : 'bg-rose-500/10 text-rose-400'
                          }`}
                        >
                          {tx.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Notification Channels */}
      <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6">
        <h3 className="text-sm font-semibold text-white mb-4 flex items-center gap-2">
          <i className="fa-regular fa-paper-plane text-indigo-400 text-xs" />
          <span>{t('notifChannelsTitle')}</span>
        </h3>
        <form onSubmit={handleSaveNotificationPrefs} className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            {[
              ['notifyEmail', 'notifyEmailLabel', 'fa-envelope'],
              ['notifyTelegram', 'notifyTelegramLabel', 'fa-brands fa-telegram'],
              ['notifyDiscord', 'notifyDiscordLabel', 'fa-brands fa-discord']
            ].map(([key, labelKey, iconClass]) => (
              <label
                key={key}
                className="flex items-center justify-between p-3 rounded-lg bg-[#070b14] border border-white/[0.06] cursor-pointer"
              >
                <span className="text-xs text-slate-200 flex items-center gap-2">
                  <i className={`${iconClass.includes('fa-brands') ? iconClass : `fa-regular ${iconClass}`} text-indigo-400`} />
                  {t(labelKey)}
                </span>
                <input
                  type="checkbox"
                  checked={!!notifPrefs[key]}
                  onChange={(e) => setNotifPrefs((prev) => ({ ...prev, [key]: e.target.checked }))}
                  className="accent-indigo-500 w-4 h-4 cursor-pointer"
                />
              </label>
            ))}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5">
                {t('telegramChatIdLabel')}
              </label>
              <input
                type="text"
                value={notifPrefs.telegramChatId || ''}
                onChange={(e) => setNotifPrefs((prev) => ({ ...prev, telegramChatId: e.target.value }))}
                placeholder={t('telegramChatIdPlaceholder')}
                className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5">
                {t('discordWebhookLabel')}
              </label>
              <input
                type="url"
                value={notifPrefs.discordWebhookUrl || ''}
                onChange={(e) => setNotifPrefs((prev) => ({ ...prev, discordWebhookUrl: e.target.value }))}
                placeholder={t('discordWebhookPlaceholder')}
                className="w-full bg-[#070b14] border border-white/[0.08] text-slate-200 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={savingNotifPrefs}
            className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50"
          >
            {savingNotifPrefs && <i className="fa-solid fa-circle-notch fa-spin mr-1.5" />}
            {t('saveNotifPrefsBtn')}
          </button>
        </form>
      </div>

      <ProfileSecurityCards {...profileProps} t={t} />

      {/* Calm Account Deletion Section */}
      <div className="bg-[#0f172a]/50 border border-white/[0.06] rounded-xl p-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h3 className="text-sm font-semibold text-white">{t('deleteAccount')}</h3>
          <p className="text-xs text-slate-400 mt-1">{t('deleteWarn')}</p>
        </div>
        <button
          type="button"
          onClick={onOpenDeleteModal}
          className="px-4 py-2 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/25 text-xs font-semibold transition-colors shrink-0 cursor-pointer"
        >
          {t('deleteConfirmBtn')}
        </button>
      </div>
    </div>
  );
}
