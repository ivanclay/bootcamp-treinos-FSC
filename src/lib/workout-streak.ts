import dayjs from "dayjs";
import utc from "dayjs/plugin/utc.js";

import { WeekDay } from "../generated/prisma/enums.js";

dayjs.extend(utc);

const WEEKDAY_BY_DAY_INDEX: Record<number, WeekDay> = {
  0: WeekDay.SUNDAY,
  1: WeekDay.MONDAY,
  2: WeekDay.TUESDAY,
  3: WeekDay.WEDNESDAY,
  4: WeekDay.THURSDAY,
  5: WeekDay.FRIDAY,
  6: WeekDay.SATURDAY,
};

const MAX_STREAK_LOOKBACK_IN_DAYS = 365;

interface CalculateWorkoutStreakParams {
  workoutDays: Array<{ weekDay: WeekDay; isRest: boolean }>;
  completedDates: Set<string>; // YYYY-MM-DD (UTC)
  referenceDate: dayjs.Dayjs;
  planCreatedAt: Date;
}

export const getWeekDay = (date: dayjs.Dayjs): WeekDay =>
  WEEKDAY_BY_DAY_INDEX[date.day()];

export const getCompletedSessionDates = (
  workoutDays: Array<{
    sessions: Array<{ startedAt: Date; completedAt: Date | null }>;
  }>
): Set<string> =>
  new Set(
    workoutDays.flatMap((day) =>
      day.sessions
        .filter((session) => session.completedAt !== null)
        .map((session) => dayjs.utc(session.startedAt).format("YYYY-MM-DD"))
    )
  );

export const calculateWorkoutStreak = ({
  workoutDays,
  completedDates,
  referenceDate,
  planCreatedAt,
}: CalculateWorkoutStreakParams): number => {
  const planWeekDays = new Set(workoutDays.map((day) => day.weekDay));
  const restWeekDays = new Set(
    workoutDays.filter((day) => day.isRest).map((day) => day.weekDay)
  );
  const reference = referenceDate.utc().startOf("day");
  const planStart = dayjs.utc(planCreatedAt).startOf("day");

  let streak = 0;

  for (let offset = 0; offset < MAX_STREAK_LOOKBACK_IN_DAYS; offset++) {
    const day = reference.subtract(offset, "day");
    if (day.isBefore(planStart)) {
      break;
    }

    const weekDay = getWeekDay(day);
    if (!planWeekDays.has(weekDay)) {
      continue;
    }

    const isCompleted = completedDates.has(day.format("YYYY-MM-DD"));
    if (restWeekDays.has(weekDay) || isCompleted) {
      streak++;
      continue;
    }

    // O dia de referência (hoje) ainda pode ser concluído: não quebra a sequência.
    if (offset === 0) {
      continue;
    }

    break;
  }

  return streak;
};
