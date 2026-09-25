import z from "zod";

import { ExternalServiceError } from "../errors/index.js";
import { env } from "../lib/env.js";

const YOUTUBE_SEARCH_URL = "https://www.googleapis.com/youtube/v3/search";
const MAX_VIDEOS = 3;

const YouTubeSearchResponseSchema = z.object({
  items: z.array(
    z.object({
      id: z.object({ videoId: z.string().min(1) }),
      snippet: z.object({
        title: z.string(),
        channelTitle: z.string(),
        thumbnails: z.object({
          high: z.object({ url: z.url() }).optional(),
          medium: z.object({ url: z.url() }).optional(),
          default: z.object({ url: z.url() }).optional(),
        }),
      }),
    })
  ),
});

interface InputDto {
  exerciseName: string;
}

interface OutputDto {
  videos: Array<{
    title: string;
    channelTitle: string;
    url: string;
    thumbnailUrl: string;
  }>;
  searchUrl: string;
}

const buildSearchQuery = (exerciseName: string): string =>
  `${exerciseName.trim()} execução correta`;

export const buildExerciseVideoSearchUrl = (exerciseName: string): string => {
  const url = new URL("https://www.youtube.com/results");
  url.searchParams.set("search_query", buildSearchQuery(exerciseName));
  return url.toString();
};

export class SearchExerciseVideos {
  async execute(dto: InputDto): Promise<OutputDto> {
    const searchUrl = buildExerciseVideoSearchUrl(dto.exerciseName);

    if (!env.YOUTUBE_API_KEY) {
      return { videos: [], searchUrl };
    }

    const url = new URL(YOUTUBE_SEARCH_URL);
    url.searchParams.set("part", "snippet");
    url.searchParams.set("type", "video");
    url.searchParams.set("maxResults", String(MAX_VIDEOS));
    url.searchParams.set("relevanceLanguage", "pt");
    url.searchParams.set("safeSearch", "strict");
    url.searchParams.set("q", buildSearchQuery(dto.exerciseName));
    url.searchParams.set("key", env.YOUTUBE_API_KEY);

    const response = await fetch(url);
    if (!response.ok) {
      throw new ExternalServiceError(
        `YouTube search failed with status ${response.status}`
      );
    }

    const parsed = YouTubeSearchResponseSchema.safeParse(await response.json());
    if (!parsed.success) {
      throw new ExternalServiceError("Unexpected YouTube search response");
    }

    return {
      videos: parsed.data.items.map((item) => {
        const { thumbnails } = item.snippet;
        return {
          title: item.snippet.title,
          channelTitle: item.snippet.channelTitle,
          url: `https://www.youtube.com/watch?v=${item.id.videoId}`,
          thumbnailUrl:
            thumbnails.high?.url ??
            thumbnails.medium?.url ??
            thumbnails.default?.url ??
            `https://i.ytimg.com/vi/${item.id.videoId}/hqdefault.jpg`,
        };
      }),
      searchUrl,
    };
  }
}
