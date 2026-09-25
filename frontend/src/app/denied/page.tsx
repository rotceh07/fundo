import Link from "next/link";

type Props = {
  searchParams: Promise<{ reason?: string | string[] }>;
};

const FALLBACK_REASON = "Your application was not approved.";

// Shows the reason returned by the backend. It is rendered as plain text, never as HTML.
export default async function DeniedPage({ searchParams }: Props) {
  const { reason } = await searchParams;
  const candidate = Array.isArray(reason) ? reason[0] : reason;
  const message = candidate?.trim() || FALLBACK_REASON;

  return (
    <section className="card result">
      <span className="result-badge result-badge-danger">Not approved</span>
      <h1>Application not approved</h1>
      <p>{message}</p>

      <Link href="/" className="button">
        Start a new application
      </Link>
    </section>
  );
}
