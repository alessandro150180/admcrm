"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth, isSoloDirezione, isCliente } from "@/lib/auth-context";

interface VoceMenu {
  href: string;
  label: string;
  visibile?: (ruolo: string | undefined) => boolean;
}

const VOCI: VoceMenu[] = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/agenti", label: "Agenti" },
  { href: "/clienti", label: "Clienti" },
  { href: "/ordini", label: "Ordini" },
  { href: "/fornitori", label: "Fornitori", visibile: isSoloDirezione },
  { href: "/attivita", label: "Attività" },
  { href: "/obiettivi", label: "Obiettivi di vendita" },
  { href: "/comunicazioni", label: "Comunicazioni" },
  { href: "/import", label: "Import Excel", visibile: isSoloDirezione },
  { href: "/notifiche", label: "Notifiche" },
  { href: "/audit-log", label: "Audit log", visibile: isSoloDirezione },
  { href: "/impostazioni", label: "Impostazioni" },
];

// Un account Cliente vede solo queste voci: il proprio fatturato, i propri ordini, le comunicazioni e il cambio password.
const VOCI_CLIENTE = ["/dashboard", "/ordini", "/comunicazioni", "/impostazioni"];

export function Sidebar() {
  const pathname = usePathname();
  const { utente, logout } = useAuth();

  return (
    <aside className="flex h-screen w-60 shrink-0 flex-col border-r border-zinc-200 bg-white">
      <div className="border-b border-zinc-200 px-5 py-4">
        {/* eslint-disable-next-line @next/next/no-img-element -- SVG locale, l'ottimizzazione next/image è superflua */}
        <img src="/logo-admgroup.svg" alt="ADM Group" className="h-12 w-auto" />
        <p className="mt-1 text-xs font-semibold tracking-wide text-teal-800">ADMcrm</p>
      </div>

      <nav className="flex-1 space-y-0.5 overflow-y-auto px-3 py-4">
        {VOCI.filter((v) => (isCliente(utente?.ruolo) ? VOCI_CLIENTE.includes(v.href) : !v.visibile || v.visibile(utente?.ruolo))).map((voce) => {
          const attivo = pathname === voce.href || pathname.startsWith(`${voce.href}/`);
          return (
            <Link
              key={voce.href}
              href={voce.href}
              className={`block rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                attivo ? "bg-teal-50 text-teal-800" : "text-zinc-600 hover:bg-zinc-100"
              }`}
            >
              {voce.label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-zinc-200 px-4 py-3">
        <p className="truncate text-sm font-medium text-zinc-800">{utente?.nome} {utente?.cognome}</p>
        <p className="truncate text-xs text-zinc-500">{utente?.ruolo}</p>
        <button onClick={logout} className="mt-2 text-xs font-medium text-teal-700 hover:underline">
          Esci
        </button>
      </div>
    </aside>
  );
}
