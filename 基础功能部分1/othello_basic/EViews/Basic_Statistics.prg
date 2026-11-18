' Optional offline basic game statistics, no AI.
' Open/import a workfile first. Each observation is a completed game.
' Required numeric series: black_count, white_count, duration_s.
' Only run with valid completed-game data (no missing values).
smpl @all
series black_win = (black_count > white_count)
series white_win = (white_count > black_count)
series draw_game = (black_count = white_count)
scalar black_win_pct = 100*@mean(black_win)
scalar white_win_pct = 100*@mean(white_win)
scalar draw_pct = 100*@mean(draw_game)
scalar avg_duration_s = @mean(duration_s)
group basic_stats black_count white_count duration_s
show basic_stats.stats
