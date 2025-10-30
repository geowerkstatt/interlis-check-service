import { describe, expect, test } from "vitest";
import { createLogHierarchy } from "./logHierarchy";
import i18n from "./i18n";

describe("transform log data to hierarchy", () => {
  test("constraint pass", () => {
    const data = [
      {
        tid: "o1",
        message: "validate set constraint ModelA.TopicA.ClassA.ConstraintName...",
        type: "Info",
      },
    ];

    const expected = [
      {
        message: "ModelA",
        type: "Info",
        values: [
          {
            message: "TopicA.ClassA",
            type: "Info",
            values: [{ message: "Set Constraint ConstraintName", type: "Info" }],
          },
        ],
      },
    ];

    const hierarchy = createLogHierarchy(data);

    expect(hierarchy).toStrictEqual(expected);
  });

  test("constraint fail", () => {
    const data = [
      {
        tid: "o1",
        message: "validate existence constraint ModelA.TopicA.ClassA.ConstraintName...",
        type: "Info",
      },
      {
        tid: "o2",
        message: "Some other message",
        type: "Info",
      },
      {
        tid: "o3",
        message:
          "Existence constraint ModelA.TopicA.ClassA.ConstraintName (EXISTENCE CONSTRAINT Test REQUIRED IN OtherClass:Test;) is violated! The value of the attribute Test of t1 was not found in the condition class.",
        type: "Error",
      },
    ];

    const expected = [
      {
        message: "ModelA",
        type: "Error",
        values: [
          {
            message: "TopicA.ClassA",
            type: "Error",
            values: [
              {
                message:
                  "Existence constraint ConstraintName is violated! The value of the attribute Test of t1 was not found in the condition class.",
                type: "Error",
              },
            ],
          },
        ],
      },
    ];

    const hierarchy = createLogHierarchy(data);

    expect(hierarchy).toStrictEqual(expected);
  });

  test("mixed log types", () => {
    const data = [
      {
        tid: "o1",
        message: "validate data...",
        type: "Info",
      },
      {
        tid: "o2",
        message: "lookup model <Base> 2.3 in repository <http://models.geo.admin.ch/>",
        type: "Info",
      },
      {
        tid: "o3",
        message: "validate mandatory constraint ModelA.TopicA.ClassA.ConstraintName...",
        type: "Info",
      },
      {
        tid: "o4",
        message:
          "MandatoryConstraint ModelA.TopicA.ClassA.ConstraintName of ModelA.TopicA.ClassA is not yet implemented.",
        type: "Warning",
      },
      {
        tid: "o5",
        message: "validate mandatory constraint ModelA.TopicA.ClassA.Constraint1...",
        type: "Info",
      },
      {
        tid: "o6",
        message: "validate mandatory constraint ModelA.TopicA.ClassA.Constraint2...",
        type: "Info",
      },
      {
        tid: "o7",
        message: "Custom message for constraint. ModelA.TopicA.ClassA.Constraint1 (MANDATORY CONSTRAINT DEFINED(Abc);)",
        type: "Error",
      },
    ];

    const expected = [
      {
        message: "ModelA",
        type: "Error",
        values: [
          {
            message: "TopicA.ClassA",
            type: "Error",
            values: [
              {
                message: "MandatoryConstraint ConstraintName of ModelA.TopicA.ClassA is not yet implemented.",
                type: "Warning",
              },
              { message: "Custom message for constraint.", type: "Error" },
              { message: "Mandatory Constraint Constraint2", type: "Info" },
            ],
          },
        ],
      },
    ];

    const hierarchy = createLogHierarchy(data);

    expect(hierarchy).toStrictEqual(expected);
  });

  test("additional warnings", () => {
    const data = [
      {
        tid: "o1",
        message: "validate set constraint ModelA.TopicA.ClassA.ConstraintName...",
        type: "Info",
      },
      {
        tid: "o2",
        message: "Duplicate warning",
        type: "Warning",
      },
      {
        tid: "o3",
        message: "Another warning",
        type: "Warning",
      },
      {
        tid: "o4",
        message: "Duplicate warning",
        type: "Warning",
      },
    ];

    const expected = [
      {
        message: "ModelA",
        type: "Info",
        values: [
          {
            message: "TopicA.ClassA",
            type: "Info",
            values: [{ message: "Set Constraint ConstraintName", type: "Info" }],
          },
        ],
      },
      {
        message: i18n.t("logHierarchy.otherMessages"),
        type: "Warning",
        values: [
          { message: "Duplicate warning", type: "Warning" },
          { message: "Another warning", type: "Warning" },
        ],
      },
    ];

    const hierarchy = createLogHierarchy(data);

    expect(hierarchy).toStrictEqual(expected);
  });

  test("additional warnings and errors", () => {
    const data = [
      {
        tid: "o1",
        message: "Duplicate warning",
        type: "Warning",
      },
      {
        tid: "o2",
        message: "Another warning",
        type: "Warning",
      },
      {
        tid: "o3",
        message: "Duplicate warning",
        type: "Warning",
      },
      {
        tid: "o4",
        message: "Some error",
        type: "Error",
      },
    ];

    const expected = [
      {
        message: i18n.t("logHierarchy.otherMessages"),
        type: "Error",
        values: [
          { message: "Some error", type: "Error" },
          { message: "Duplicate warning", type: "Warning" },
          { message: "Another warning", type: "Warning" },
        ],
      },
    ];

    const hierarchy = createLogHierarchy(data);

    expect(hierarchy).toStrictEqual(expected);
  });
});
