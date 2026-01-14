export type User = {
    id: string;
    displayName: string;
    email: string;
    accessToken: string;
    refreshToken: string;
    imageUrl?: string;
    roles: string[];
}

export type LoginCreds = {
    email: string;
    password: string;
}
