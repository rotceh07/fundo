// The browser calls the .NET API directly; CORS on the backend allows this origin.
const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:5176";

export type SubmitApplicationPayload = {
  firstName: string;
  lastName: string;
  address: string;
  state: string;
  companyName: string;
  requestedAmount: number;
  ssn: string;
};

export type SubmitApplicationResponse = {
  isApproved: boolean;
  applicationId: string | null;
  denialReason: string | null;
};

export type ApplicationField = keyof SubmitApplicationPayload;

export type SubmitApplicationApiResult =
  | { kind: "success"; data: SubmitApplicationResponse }
  | {
      kind: "validation";
      fieldErrors: Partial<Record<ApplicationField, string>>;
      hasOtherErrors: boolean;
    }
  | { kind: "server-error" };

// Maps the backend validation keys to form fields. Any other key, such as a JSON
// conversion error like "$.requestedAmount", is reported as a generic error instead.
const FIELD_MAP: Record<string, ApplicationField> = {
  FirstName: "firstName",
  LastName: "lastName",
  Address: "address",
  State: "state",
  CompanyName: "companyName",
  RequestedAmount: "requestedAmount",
  Ssn: "ssn",
};

// Network failures are not caught here; fetch rejects and the caller shows a generic error.
export async function submitApplication(
  payload: SubmitApplicationPayload,
): Promise<SubmitApplicationApiResult> {
  const response = await fetch(`${API_BASE_URL}/api/applications`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  if (response.ok) {
    const data: unknown = await response.json().catch(() => null);
    return isSubmitApplicationResponse(data) ? { kind: "success", data } : { kind: "server-error" };
  }

  if (response.status === 400) {
    const problem: unknown = await response.json().catch(() => null);
    return toValidationResult(problem);
  }

  return { kind: "server-error" };
}

// Guards against an unexpected response shape so the UI never navigates on inconsistent data.
function isSubmitApplicationResponse(value: unknown): value is SubmitApplicationResponse {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  if (typeof candidate.isApproved !== "boolean") {
    return false;
  }

  if (candidate.isApproved) {
    return typeof candidate.applicationId === "string" && candidate.applicationId.length > 0;
  }

  return candidate.denialReason === null || typeof candidate.denialReason === "string";
}

function toValidationResult(problem: unknown): SubmitApplicationApiResult {
  const fieldErrors: Partial<Record<ApplicationField, string>> = {};
  let hasOtherErrors = false;

  const errors =
    typeof problem === "object" && problem !== null
      ? (problem as { errors?: unknown }).errors
      : undefined;

  if (typeof errors !== "object" || errors === null) {
    return { kind: "validation", fieldErrors, hasOtherErrors: true };
  }

  for (const [key, messages] of Object.entries(errors)) {
    const field = FIELD_MAP[key];
    const firstMessage = Array.isArray(messages) ? messages[0] : undefined;

    if (field && typeof firstMessage === "string") {
      fieldErrors[field] ??= firstMessage;
    } else {
      hasOtherErrors = true;
    }
  }

  return { kind: "validation", fieldErrors, hasOtherErrors };
}
