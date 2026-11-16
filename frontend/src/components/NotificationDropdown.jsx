import { useEffect, useRef } from 'react';
import { formatNotifTime, localizeNotif } from '../utils/formatters';

export function NotificationDropdown({
  open,
  setOpen,
  notifications,
  unreadCount,
  lang,
  t,
  markNotifRead,
  markAllNotifsRead,
  clearReadNotifs,
  onOpenAll
}) {
  const dropdownRef = useRef(null);

  useEffect(() => {
    const handleOutside = (e) => {
      if (open && dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, [open, setOpen]);

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={() => setOpen((prev) => !prev)}
        className="relative text-slate-400 hover:text-white transition-colors text-xs font-medium flex items-center justify-center w-9 h-9 bg-slate-900/60 hover:bg-slate-800/80 rounded-lg border border-white/[0.08] cursor-pointer"
        aria-label={t('notifications')}
      >
        <i className="fa-regular fa-bell text-sm" />
        {unreadCount > 0 && (
          <span className="absolute -top-1.5 -right-1.5 bg-rose-500 text-white text-[9px] font-bold px-1.5 py-0.5 rounded-full min-w-[18px] text-center shadow-sm">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 mt-2 w-80 sm:w-96 bg-[#0f172a] border border-white/[0.08] rounded-xl shadow-2xl z-50 overflow-hidden fade-enter">
          <div className="px-4 py-3 border-b border-white/[0.06] flex items-center justify-between gap-2 bg-[#070b14]/60">
            <span className="text-xs font-semibold text-white">{t('notifications')}</span>
            <div className="flex items-center gap-2.5">
              {unreadCount > 0 && (
                <button
                  onClick={markAllNotifsRead}
                  className="text-[11px] text-indigo-400 hover:text-indigo-300 font-medium cursor-pointer"
                >
                  {t('markAllRead')}
                </button>
              )}
              {notifications.some((n) => n.isRead) && (
                <button
                  onClick={clearReadNotifs}
                  className="text-[11px] text-slate-400 hover:text-slate-200 font-medium cursor-pointer"
                >
                  {t('clearRead')}
                </button>
              )}
            </div>
          </div>

          <div className="max-h-80 overflow-y-auto divide-y divide-white/[0.04]">
            {notifications.length === 0 ? (
              <div className="py-10 px-4 text-center">
                <i className="fa-regular fa-bell-slash text-slate-600 text-lg mb-2" />
                <p className="text-xs font-medium text-slate-300">{t('noNotificationsTitle')}</p>
                <p className="text-[11px] text-slate-500 mt-1">{t('noNotificationsDesc')}</p>
              </div>
            ) : (
              notifications.slice(0, 8).map((n) => {
                const { title, message } = localizeNotif(n, lang);
                return (
                  <div
                    key={n.id}
                    onClick={() => !n.isRead && markNotifRead(n.id)}
                    className={`p-3.5 hover:bg-white/[0.03] transition-colors cursor-pointer ${
                      !n.isRead ? 'bg-indigo-500/[0.05]' : ''
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          {!n.isRead && <span className="w-1.5 h-1.5 rounded-full bg-indigo-400 shrink-0" />}
                          <p className="text-xs font-semibold text-slate-200 truncate">{title}</p>
                        </div>
                        <p className="text-[11px] text-slate-400 mt-1 line-clamp-2">{message}</p>
                        <p className="text-[10px] text-slate-500 mt-1.5 font-mono">
                          {formatNotifTime(n.createdAt, lang)}
                        </p>
                      </div>
                      {n.relatedUrl && (
                        <a
                          href={n.relatedUrl}
                          target="_blank"
                          rel="noreferrer"
                          onClick={(e) => e.stopPropagation()}
                          className="text-indigo-400 hover:text-indigo-300 text-[11px] shrink-0 p-1"
                          title={t('goTicket')}
                        >
                          <i className="fa-solid fa-arrow-up-right-from-square" />
                        </a>
                      )}
                    </div>
                  </div>
                );
              })
            )}
          </div>

          <div className="px-4 py-2.5 border-t border-white/[0.06] bg-[#070b14]/60 text-center">
            <button
              onClick={() => {
                setOpen(false);
                onOpenAll();
              }}
              className="text-[11px] font-medium text-indigo-400 hover:text-indigo-300 cursor-pointer"
            >
              {t('viewAllNotifs')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
