import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Fundo | Loan Application",
  description: "Submit a loan application and receive an eligibility decision.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <div className="page">
          <header className="brand">
            <span className="brand-mark" aria-hidden="true">
              F
            </span>
            <span className="brand-name">Fundo</span>
          </header>
          <main>{children}</main>
        </div>
      </body>
    </html>
  );
}
