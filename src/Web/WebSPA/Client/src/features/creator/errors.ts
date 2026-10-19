import { DomainError } from 'src/communication-types';
import { formatErrorMessage } from 'src/utils/error-utils';
import { serializeRequestError } from 'src/utils/error-result';

/** The text for an error of a request to the server: the message of the server, or that it cannot be reached. */
export const requestErrorMessage = (error: unknown) =>
   formatErrorMessage(serializeRequestError(error) as unknown as DomainError);
