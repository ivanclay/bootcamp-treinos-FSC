-- CreateEnum
CREATE TYPE "WorkoutGoal" AS ENUM ('HYPERTROPHY', 'STRENGTH', 'HYPERTROPHY_AND_STRENGTH', 'WEIGHT_LOSS', 'CONDITIONING', 'HEALTH');

-- AlterTable
ALTER TABLE "WorkoutPlan" ADD COLUMN     "coverImageUrl" TEXT,
ADD COLUMN     "goal" "WorkoutGoal";
