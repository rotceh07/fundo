import { ApplicationForm } from "@/components/application-form";

export default function HomePage() {
  return (
    <>
      <section className="intro">
        <h1>Loan application</h1>
        <p>Complete the form below to receive an eligibility decision. All fields are required.</p>
      </section>
      <ApplicationForm />
    </>
  );
}
