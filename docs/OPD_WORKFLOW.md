# OPD Workflow — Registration → Vitals Station → Doctor Lobby → Consultation

**SRS addendum for §3.2 (Appointments) and §3.3 (OPD).** The original spec described
*booking* and *consultation* as two steps and let the doctor record vitals inline. That
skipped the **vitals station** and the **queue that a doctor's waiting lobby is built on**,
and never linked a consultation back to its appointment/token. This document is the
corrected process model; the code now implements it.

## The corrected flow

```
Register patient (UHID)                         [§3.1]
        │
        ▼
Book OPD  — select patient + doctor → token      [§3.2]   Appointment.Status = Booked
        │
        ▼
Vitals station — a vitals-taking attendant       [§3.2a, NEW]
records temp/pulse/BP/SpO₂/resp/weight                     Appointment.Status = VitalsDone
        │                                                  (patient now in the doctor's lobby)
        ▼
Doctor's waiting lobby — patients with vitals     [§3.3]   queue filtered by
done, for this doctor; doctor SELECTS one                  doctorCode + status=VitalsDone
        │
        ▼
Consultation — vitals preloaded (read-only),      [§3.3]   Appointment.Status = Completed
diagnosis, prescription, orders, advice → Save             (station vitals linked to the
                                                            encounter; token closed)
```

## Appointment status lifecycle

| Status | Set when | Meaning |
|---|---|---|
| `Booked` | appointment booked (patient + doctor + token) | awaiting vitals |
| `VitalsDone` | attendant records vitals at the station | **in the doctor's waiting lobby** |
| `Completed` | doctor saves the consultation | token closed, encounter written |

(`InConsultation` is reserved for a future "called in" state; the current build advances
`VitalsDone → Completed` on save.)

## Role separation

| Role | Step | Screen |
|---|---|---|
| Receptionist / front office | Book OPD (select patient + doctor) | **Appointments** |
| **Vitals attendant** | Record vitals for a booked patient | **Appointments → Vitals Station** |
| Doctor | Pick a waiting patient from the lobby, consult | **OPD Consultation → Waiting Lobby** |

RBAC grants can scope these separately (e.g. a dedicated `vitals_attendant` role limited to
the vitals action); today all three are available to front-office/clinical roles.

## Data model

- `clinical.Appointment.Status` drives the lifecycle (`Booked → VitalsDone → Completed`).
- `clinical.Vitals` — `EncounterId` is **nullable** and a nullable **`AppointmentId`** was
  added, so vitals can be recorded **at the station before any encounter exists**. When the
  doctor saves the consultation, the station vitals are **linked** to the new encounter
  (`Vitals.EncounterId` set) and the appointment is **Completed**. (Migration `M0005`.)

## API surface

| Endpoint | Purpose |
|---|---|
| `POST /api/appointments` | Book (patient + doctor → token, `Booked`) |
| `POST /api/appointments/{id}/vitals` | **Vitals station** — record vitals → `VitalsDone` |
| `GET  /api/appointments/{id}/vitals` | Preload station vitals for the doctor (read-only) |
| `GET  /api/appointments/queue?doctorCode=&status=VitalsDone` | **Doctor's waiting lobby** |
| `POST /api/encounters/consultation` (+ `appointmentId`) | Consult → link vitals + `Completed` |

## What was wrong before (root cause)

1. The OPD consultation screen rendered a **static/default patient** (`HIS.mock.currentPatient`)
   — the fixed UHID — with **no way to select** the booked patient.
2. **Vitals were bundled** into the doctor's `SaveConsultation`; there was **no station** and
   no attendant step, so nothing gated "vitals done → enter the queue."
3. The **appointment lifecycle was degenerate** (only `Booked`), so a "doctor's waiting lobby"
   could not be derived.
4. `SaveConsultation` took a raw UHID + doctor code and **never referenced the AppointmentId**,
   so consulting never closed the token — booking and consultation were disconnected.

All four are fixed by the flow above.
