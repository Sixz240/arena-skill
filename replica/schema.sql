-- Banco local do jogo (SQLite, dentro do celular). Não há servidor.
-- Dois arquivos:
--   data.db  -> times, jogadores, ligas. Vem junto no APK e é atualizado pelo
--               "Atualizar elencos" (Wikipédia) e pelo Editor (S16).
--   save.db  -> campeonatos, copas e carreiras do usuário.
-- Convenções (adaptadas do Postgres para SQLite):
--   ids são texto (UUID gerado no C# com System.Guid), datas são texto ISO 8601 em UTC,
--   dinheiro é inteiro em centavos com coluna de moeda, enums viram CHECK.
--   Um só usuário, offline: não há dono por linha nem RLS. Todo acesso passa pela
--   camada de dados (Repositories) no C#.
PRAGMA foreign_keys = ON;

-- ============================== data.db ==============================

create table leagues (
  id text primary key,                 -- ex.: 'bra-serie-a'
  name text not null,                  -- 'Brasileirão Série A'
  country text not null,
  kind text not null check (kind in ('club_league','national_teams','continental')),
  season text not null,                -- '2026'
  format text not null check (format in ('round_robin','knockout','groups_knockout')),
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

create table teams (
  id text primary key,                 -- ex.: 'flamengo'
  name text not null,
  short_name text not null check (length(short_name) <= 4),  -- 'FLA' no placar
  league_id text references leagues(id) on delete set null,  -- seleções: liga 'national_teams'
  is_national integer not null default 0 check (is_national in (0,1)),
  wiki_page text not null,             -- página da Wikipédia (en) para escudo e elenco
  crest_url text,                      -- cache da última URL de escudo aceita
  crest_status text not null default 'unknown'
    check (crest_status in ('unknown','crest','flag','colors','manual')),
  crest_override text,                 -- 'bandeira' ou URL definida no Editor
  rating_attack integer not null check (rating_attack between 1 and 99),
  rating_midfield integer not null check (rating_midfield between 1 and 99),
  rating_defense integer not null check (rating_defense between 1 and 99),
  stadium_name text,
  budget_cents integer not null default 0,
  currency text not null default 'EUR',
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index teams_league on teams (league_id);
create index teams_name on teams (name);

-- Uniformes: casa, fora, goleiro. Viram textura gerada em tempo real.
create table kits (
  id text primary key,
  team_id text not null references teams(id) on delete cascade,
  slot text not null check (slot in ('home','away','third','gk')),
  pattern text not null check (pattern in ('plain','v_stripes','h_hoops','sash','halves','chest_band','tricolor_stripes')),
  shirt_primary text not null check (shirt_primary glob '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
  shirt_secondary text,
  shirt_tertiary text,                 -- terceira cor (tricolores: São Paulo, Fluminense, Grêmio)
  shorts text not null,
  socks text not null,
  number_color text not null,
  season text not null,
  source_url text,                     -- onde a cor/padrão foi conferido
  unique (team_id, slot)
);
create index kits_team on kits (team_id);

create table players (
  id text primary key,
  team_id text references teams(id) on delete set null,   -- null = sem clube (agente livre)
  name text not null,                  -- nome real (uso pessoal)
  display_name text not null,          -- nome na camisa / placar
  shirt_number integer check (shirt_number between 1 and 99),
  position text not null check (position in ('GK','RB','CB','LB','DM','CM','AM','RW','LW','ST')),
  birth_date text,                     -- 'YYYY-MM-DD'
  nationality text,
  -- Atributos próprios do jogo (NUNCA copiar as notas da EA).
  pace integer not null check (pace between 1 and 99),
  shooting integer not null check (shooting between 1 and 99),
  passing integer not null check (passing between 1 and 99),
  dribbling integer not null check (dribbling between 1 and 99),
  defending integer not null check (defending between 1 and 99),
  physical integer not null check (physical between 1 and 99),
  goalkeeping integer not null default 10 check (goalkeeping between 1 and 99),
  overall integer not null check (overall between 1 and 99),
  skin_tone integer not null default 2 check (skin_tone between 0 and 5),  -- só para o modelo low-poly
  hair_style integer not null default 0,
  source_url text,                     -- página de onde o elenco veio
  edited_by_user integer not null default 0 check (edited_by_user in (0,1)),  -- atualização não sobrescreve
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index players_team on players (team_id);
create index players_name on players (name);
create index players_overall on players (overall desc);
create unique index players_team_number on players (team_id, shirt_number) where shirt_number is not null;

-- ============================== save.db ==============================

create table competitions (
  id text primary key,
  kind text not null check (kind in ('friendly','league','cup','career_league','career_cup')),
  name text not null,
  league_id text,                      -- referência lógica ao data.db (outro arquivo: sem FK)
  season text not null,
  status text not null default 'in_progress' check (status in ('in_progress','finished','abandoned')),
  current_round integer not null default 1,
  career_id text references careers(id) on delete cascade,
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index competitions_career on competitions (career_id);
create index competitions_status on competitions (status, updated_at desc);

create table competition_teams (
  competition_id text not null references competitions(id) on delete cascade,
  team_id text not null,
  group_name text,                     -- copas com grupos
  primary key (competition_id, team_id)
);

create table matches (
  id text primary key,
  competition_id text references competitions(id) on delete cascade,
  round integer not null,
  stage text not null default 'league'
    check (stage in ('league','group','r32','r16','qf','sf','final','third','friendly')),
  home_team_id text not null,
  away_team_id text not null,
  status text not null default 'scheduled' check (status in ('scheduled','played','simulated')),
  home_goals integer check (home_goals >= 0),
  away_goals integer check (away_goals >= 0),
  went_extra_time integer not null default 0,
  home_pens integer, away_pens integer,
  stats_json text not null default '{}',   -- posse, chutes, faltas...
  played_at text,
  check (home_team_id <> away_team_id),
  check (status = 'scheduled' or (home_goals is not null and away_goals is not null))
);
create index matches_competition_round on matches (competition_id, round);
create unique index matches_unique_fixture on matches (competition_id, round, home_team_id, away_team_id);

create table match_events (
  id text primary key,
  match_id text not null references matches(id) on delete cascade,
  minute integer not null check (minute between 0 and 130),
  kind text not null check (kind in ('goal','own_goal','penalty_goal','yellow','red','sub_in','sub_out','injury')),
  team_id text not null,
  player_id text
);
create index match_events_match on match_events (match_id, minute);

create table standings (
  competition_id text not null references competitions(id) on delete cascade,
  team_id text not null,
  group_name text,
  played integer not null default 0, won integer not null default 0,
  drawn integer not null default 0, lost integer not null default 0,
  goals_for integer not null default 0, goals_against integer not null default 0,
  points integer not null default 0,
  primary key (competition_id, team_id)
);
create index standings_order on standings (competition_id, points desc, goals_for - goals_against desc, goals_for desc);

create table careers (
  id text primary key,
  team_id text not null,
  manager_name text not null,
  season text not null,
  budget_cents integer not null,
  currency text not null default 'EUR',
  transfer_window_open integer not null default 1 check (transfer_window_open in (0,1)),
  board_confidence integer not null default 50 check (board_confidence between 0 and 100),
  status text not null default 'active' check (status in ('active','sacked','retired')),
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

-- Estado dos jogadores dentro de uma carreira (o data.db não muda durante a carreira).
create table career_players (
  career_id text not null references careers(id) on delete cascade,
  player_id text not null,
  team_id text,                        -- clube atual nesta carreira
  overall integer not null check (overall between 1 and 99),
  potential integer not null check (potential between 1 and 99),
  age integer not null,
  morale integer not null default 50 check (morale between 0 and 100),
  form integer not null default 50 check (form between 0 and 100),
  injury_until_round integer,
  suspended_rounds integer not null default 0,
  contract_until text not null,        -- temporada
  wage_cents integer not null,
  value_cents integer not null,
  primary key (career_id, player_id)
);
create index career_players_team on career_players (career_id, team_id);

create table transfers (
  id text primary key,
  career_id text not null references careers(id) on delete cascade,
  player_id text not null,
  from_team_id text, to_team_id text,
  fee_cents integer not null check (fee_cents >= 0),
  status text not null check (status in ('offered','accepted','rejected','completed')),
  season text not null,
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index transfers_career on transfers (career_id, status);

create table settings (
  key text primary key,                -- 'controls','difficulty','half_minutes','sound','language'
  value text not null
);
