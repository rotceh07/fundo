"use client";

import { useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { submitApplication, type ApplicationField } from "@/lib/api";
import { isUsStateCode, US_STATES } from "@/lib/states";

type FormValues = Record<ApplicationField, string>;

type FieldErrors = Partial<Record<ApplicationField, string>>;

const INITIAL_VALUES: FormValues = {
  firstName: "",
  lastName: "",
  address: "",
  state: "",
  companyName: "",
  requestedAmount: "",
  ssn: "",
};

// Same rule as the backend: nine ASCII digits, optionally separated by spaces or dashes.
const SSN_PATTERN = /^\s*(?:[0-9][ -]*){9}\s*$/;

const FIELD_ORDER: ApplicationField[] = [
  "firstName",
  "lastName",
  "address",
  "state",
  "ssn",
  "companyName",
  "requestedAmount",
];

const REQUIRED_MESSAGES: Record<ApplicationField, string> = {
  firstName: "First name is required.",
  lastName: "Last name is required.",
  address: "Address is required.",
  state: "State is required.",
  companyName: "Company name is required.",
  requestedAmount: "Requested amount is required.",
  ssn: "SSN is required.",
};

const GENERIC_ERROR = "We couldn't submit your application. Please try again.";

// Only checks shape and format for quick feedback. Eligibility (NY, blacklist) is decided by
// the backend, and the backend validation remains the authority.
function validate(values: FormValues): FieldErrors {
  const errors: FieldErrors = {};

  for (const field of FIELD_ORDER) {
    if (values[field].trim() === "") {
      errors[field] = REQUIRED_MESSAGES[field];
    }
  }

  if (!errors.state && !isUsStateCode(values.state)) {
    errors.state = "Select a valid state.";
  }

  if (!errors.ssn && !SSN_PATTERN.test(values.ssn)) {
    errors.ssn = "SSN must contain exactly 9 digits.";
  }

  if (!errors.requestedAmount) {
    const amount = Number(values.requestedAmount);

    if (!Number.isFinite(amount) || amount <= 0) {
      errors.requestedAmount = "Requested amount must be greater than zero.";
    }
  }

  return errors;
}

export function ApplicationForm() {
  const router = useRouter();
  const [values, setValues] = useState<FormValues>(INITIAL_VALUES);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Synchronous guard: state updates are not visible to a second click in the same tick.
  const submitLockRef = useRef(false);

  function handleChange(event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    const field = event.target.name as ApplicationField;

    setValues((current) => ({ ...current, [field]: event.target.value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setSubmissionError(null);
  }

  function focusFirstInvalid(form: HTMLFormElement, errors: FieldErrors) {
    const firstInvalid = FIELD_ORDER.find((field) => errors[field]);
    const element = firstInvalid ? form.elements.namedItem(firstInvalid) : null;

    if (element instanceof HTMLElement) {
      element.focus();
    }
  }

  function releaseLock(message: string) {
    submitLockRef.current = false;
    setIsSubmitting(false);
    setSubmissionError(message);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (submitLockRef.current) {
      return;
    }

    const form = event.currentTarget;
    setSubmissionError(null);

    const errors = validate(values);

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      focusFirstInvalid(form, errors);
      return;
    }

    submitLockRef.current = true;
    setIsSubmitting(true);

    try {
      // The SSN is only trimmed; the backend owns its canonical format.
      const result = await submitApplication({
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim(),
        address: values.address.trim(),
        state: values.state.trim(),
        companyName: values.companyName.trim(),
        requestedAmount: Number(values.requestedAmount),
        ssn: values.ssn.trim(),
      });

      if (result.kind === "success") {
        const { isApproved, applicationId, denialReason } = result.data;

        // The lock stays on: navigation unmounts the form, so a second submit is impossible.
        if (isApproved) {
          router.push(`/approved?applicationId=${encodeURIComponent(applicationId!)}`);
        } else {
          router.push(denialReason ? `/denied?reason=${encodeURIComponent(denialReason)}` : "/denied");
        }

        return;
      }

      if (result.kind === "validation") {
        setFieldErrors(result.fieldErrors);
        focusFirstInvalid(form, result.fieldErrors);
        releaseLock(
          result.hasOtherErrors && Object.keys(result.fieldErrors).length === 0
            ? "Please check the information entered and try again."
            : "Please review the highlighted fields.",
        );
        return;
      }

      releaseLock(GENERIC_ERROR);
    } catch {
      releaseLock(GENERIC_ERROR);
    }
  }

  function inputProps(field: ApplicationField, hintId?: string) {
    const error = fieldErrors[field];
    const describedBy = [hintId, error ? `${field}-error` : undefined].filter(Boolean).join(" ");

    return {
      id: field,
      name: field,
      value: values[field],
      onChange: handleChange,
      required: true,
      "aria-invalid": error ? true : false,
      "aria-describedby": describedBy || undefined,
    };
  }

  function errorFor(field: ApplicationField) {
    const error = fieldErrors[field];

    return error ? (
      <p id={`${field}-error`} className="field-error">
        {error}
      </p>
    ) : null;
  }

  return (
    <form className="card form" noValidate onSubmit={handleSubmit} aria-busy={isSubmitting}>
      {submissionError ? (
        <div className="alert" role="alert">
          {submissionError}
        </div>
      ) : null}

      <fieldset className="form-section">
        <legend>Personal information</legend>

        <div className="grid">
          <div className="field">
            <label htmlFor="firstName">First name</label>
            <input type="text" autoComplete="given-name" {...inputProps("firstName")} />
            {errorFor("firstName")}
          </div>

          <div className="field">
            <label htmlFor="lastName">Last name</label>
            <input type="text" autoComplete="family-name" {...inputProps("lastName")} />
            {errorFor("lastName")}
          </div>
        </div>

        <div className="field">
          <label htmlFor="address">Street address</label>
          <input type="text" autoComplete="street-address" {...inputProps("address", "address-hint")} />
          <p id="address-hint" className="hint">
            State is selected separately below.
          </p>
          {errorFor("address")}
        </div>

        <div className="field">
          <label htmlFor="state">State</label>
          <select autoComplete="address-level1" {...inputProps("state")}>
            <option value="">Select a state</option>
            {US_STATES.map((state) => (
              <option key={state.code} value={state.code}>
                {state.name} ({state.code})
              </option>
            ))}
          </select>
          {errorFor("state")}
        </div>

        <div className="field">
          <label htmlFor="ssn">Social Security Number</label>
          <input
            type="text"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            placeholder="123-45-6789"
            {...inputProps("ssn", "ssn-hint")}
          />
          <p id="ssn-hint" className="hint">
            9 digits. Hyphens or spaces are allowed. We use your SSN to identify returning
            applications and evaluate eligibility.
          </p>
          {errorFor("ssn")}
        </div>
      </fieldset>

      <fieldset className="form-section">
        <legend>Application details</legend>

        <div className="grid">
          <div className="field">
            <label htmlFor="companyName">Company name</label>
            <input type="text" autoComplete="organization" {...inputProps("companyName")} />
            {errorFor("companyName")}
          </div>

          <div className="field">
            <label htmlFor="requestedAmount">Requested amount</label>
            <div className="input-prefix">
              <span aria-hidden="true">$</span>
              <input type="number" step="any" inputMode="decimal" {...inputProps("requestedAmount")} />
            </div>
            {errorFor("requestedAmount")}
          </div>
        </div>
      </fieldset>

      <button type="submit" className="button" disabled={isSubmitting} aria-disabled={isSubmitting}>
        {isSubmitting ? (
          <>
            <span className="spinner" aria-hidden="true" />
            Submitting...
          </>
        ) : (
          "Submit application"
        )}
      </button>
    </form>
  );
}
