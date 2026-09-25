import Link from "next/link";

type Props = {
  searchParams: Promise<{ applicationId?: string | string[] }>;
};

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

// A presentation route, not an authoritative record: the reference comes from the URL,
// so it is only shown when it looks like a real application id.
export default async function ApprovedPage({ searchParams }: Props) {
  const { applicationId } = await searchParams;
  const candidate = Array.isArray(applicationId) ? applicationId[0] : applicationId;
  const reference = candidate && GUID_PATTERN.test(candidate) ? candidate : null;

  return (
    <section className="card result">
      <span className="result-badge result-badge-success">Approved</span>
      <h1>Application approved</h1>
      <p>Your application has been approved and saved successfully.</p>

      {reference ? (
        <div className="reference">
          <span className="reference-label">Application reference</span>
          <code>{reference}</code>
        </div>
      ) : null}

      <Link href="/" className="button">
        Submit another application
      </Link>
    </section>
  );
}
