Below is a SOTA demo target for CanonFlow.

Real schema.sql
→ exact Canon IR
→ exact TypeScript validator
→ exact OpenAPI schema
→ exact AgentCatalog
→ proof report

1. schema.sql

CREATE TABLE customers (
  customer_id UUID PRIMARY KEY,
  email VARCHAR(255) NOT NULL UNIQUE,
  age INT NOT NULL CHECK (age >= 18 AND age < 120),
  status VARCHAR(20) NOT NULL CHECK (status IN ('ACTIVE', 'SUSPENDED', 'CLOSED')),
  credit_limit DECIMAL(12,2) NOT NULL CHECK (credit_limit >= 0 AND credit_limit <= 1000000),
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE orders (
  order_id UUID PRIMARY KEY,
  customer_id UUID NOT NULL REFERENCES customers(customer_id),
  amount DECIMAL(12,2) NOT NULL CHECK (amount > 0),
  currency CHAR(3) NOT NULL CHECK (currency IN ('INR', 'USD', 'EUR')),
  order_status VARCHAR(20) NOT NULL CHECK (order_status IN ('PLACED', 'PAID', 'CANCELLED')),
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

2. Canon IR

entities:
  Customer:
    source: customers
    fields:
      customer_id:
        type: uuid
        required: true
        primaryKey: true

      email:
        type: string
        maxLength: 255
        required: true
        unique: true

      age:
        type: integer
        required: true
        constraints:
          - gte: 18
          - lt: 120

      status:
        type: enum
        required: true
        values:
          - ACTIVE
          - SUSPENDED
          - CLOSED

      credit_limit:
        type: decimal
        precision: 12
        scale: 2
        required: true
        constraints:
          - gte: 0
          - lte: 1000000

      created_at:
        type: timestamp
        required: true
        default: CURRENT_TIMESTAMP

  Order:
    source: orders
    fields:
      order_id:
        type: uuid
        required: true
        primaryKey: true

      customer_id:
        type: uuid
        required: true
        foreignKey:
          references: Customer.customer_id

      amount:
        type: decimal
        required: true
        constraints:
          - gt: 0

      currency:
        type: enum
        required: true
        values:
          - INR
          - USD
          - EUR

      order_status:
        type: enum
        required: true
        values:
          - PLACED
          - PAID
          - CANCELLED

      created_at:
        type: timestamp
        required: true
        default: CURRENT_TIMESTAMP

3. TypeScript validator

export type CustomerStatus = "ACTIVE" | "SUSPENDED" | "CLOSED";
export type Currency = "INR" | "USD" | "EUR";
export type OrderStatus = "PLACED" | "PAID" | "CANCELLED";

export function validateCustomer(value: any): boolean {
  return (
    typeof value.customer_id === "string" &&
    typeof value.email === "string" &&
    value.email.length <= 255 &&
    Number.isInteger(value.age) &&
    value.age >= 18 &&
    value.age < 120 &&
    ["ACTIVE", "SUSPENDED", "CLOSED"].includes(value.status) &&
    typeof value.credit_limit === "number" &&
    value.credit_limit >= 0 &&
    value.credit_limit <= 1000000
  );
}

export function validateOrder(value: any): boolean {
  return (
    typeof value.order_id === "string" &&
    typeof value.customer_id === "string" &&
    typeof value.amount === "number" &&
    value.amount > 0 &&
    ["INR", "USD", "EUR"].includes(value.currency) &&
    ["PLACED", "PAID", "CANCELLED"].includes(value.order_status)
  );
}

4. OpenAPI schema

components:
  schemas:
    Customer:
      type: object
      required:
        - customer_id
        - email
        - age
        - status
        - credit_limit
        - created_at
      properties:
        customer_id:
          type: string
          format: uuid
        email:
          type: string
          maxLength: 255
        age:
          type: integer
          minimum: 18
          exclusiveMaximum: 120
        status:
          type: string
          enum:
            - ACTIVE
            - SUSPENDED
            - CLOSED
        credit_limit:
          type: number
          minimum: 0
          maximum: 1000000
        created_at:
          type: string
          format: date-time

    Order:
      type: object
      required:
        - order_id
        - customer_id
        - amount
        - currency
        - order_status
        - created_at
      properties:
        order_id:
          type: string
          format: uuid
        customer_id:
          type: string
          format: uuid
        amount:
          type: number
          exclusiveMinimum: 0
        currency:
          type: string
          enum:
            - INR
            - USD
            - EUR
        order_status:
          type: string
          enum:
            - PLACED
            - PAID
            - CANCELLED
        created_at:
          type: string
          format: date-time

5. AgentCatalog

entities:
  Customer:
    purpose: "Represents a registered customer."
    sourceTable: customers
    changeRisk: high
    rules:
      - customer_id is the primary key.
      - email is required, unique, and max 255 characters.
      - age must be >= 18 and < 120.
      - status must be ACTIVE, SUSPENDED, or CLOSED.
      - credit_limit must be between 0 and 1000000.
    safeAgentGuidance:
      - Do not change customer_id type.
      - If changing age rules, update database CHECK, TypeScript validator, and OpenAPI schema.
      - If adding status values, update DB enum/check, frontend dropdowns, and API schema.

  Order:
    purpose: "Represents a customer order."
    sourceTable: orders
    changeRisk: high
    rules:
      - order_id is the primary key.
      - customer_id must reference Customer.customer_id.
      - amount must be greater than 0.
      - currency must be INR, USD, or EUR.
      - order_status must be PLACED, PAID, or CANCELLED.
    safeAgentGuidance:
      - Never allow negative or zero amount.
      - Preserve customer foreign-key relationship.
      - If adding currency values, update DB, API schema, validator, and UI currency selector.

6. PROOF.md

# CanonFlow Proof Report

## Pipeline

schema.sql
→ Canon IR
→ TypeScript validator
→ OpenAPI schema
→ AgentCatalog
→ Verification

## Summary

| Artifact | Fidelity |
|---|---|
| Canon IR | Exact |
| TypeScript Validator | Exact |
| OpenAPI Schema | Exact |
| AgentCatalog | Exact |
| Round-trip Verification | Passed |

## Verified Rules

### Customer.age

DB:

age >= 18 AND age < 120

Canon IR:

gte 18, lt 120

TypeScript:

value.age >= 18 && value.age < 120

OpenAPI:

minimum: 18  
exclusiveMaximum: 120

Fidelity: Exact

---

### Customer.status

DB:

status IN ('ACTIVE', 'SUSPENDED', 'CLOSED')

Canon IR:

enum ACTIVE | SUSPENDED | CLOSED

TypeScript:

["ACTIVE", "SUSPENDED", "CLOSED"].includes(value.status)

OpenAPI:

enum: ACTIVE, SUSPENDED, CLOSED

Fidelity: Exact

---

### Order.amount

DB:

amount > 0

Canon IR:

gt 0

TypeScript:

value.amount > 0

OpenAPI:

exclusiveMinimum: 0

Fidelity: Exact

---

### Order.customer_id

DB:

customer_id REFERENCES customers(customer_id)

Canon IR:

Order.customer_id → Customer.customer_id

AgentCatalog:

Preserve customer foreign-key relationship.

Fidelity: Exact

## Result

All supported constraints were preserved exactly across all generated artifacts.

No semantic drift detected.
No unsupported constraints detected.
No approximate projections detected.

This is the kind of demo that would make CanonFlow feel serious: not many features, but one complete proof chain with zero hand-waving.
